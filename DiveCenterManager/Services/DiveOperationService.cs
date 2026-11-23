using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class DiveOperationService(IDbContextFactory<DiveCenterDbContext> factory, ReservationService reservations)
{
    private static IQueryable<DiveOperation> WithDetails(DiveCenterDbContext db) => db.DiveOperations.Include(x => x.DiveSite).Include(x => x.LeadInstructor).Include(x => x.Boat).Include(x => x.Reservation);
    public async Task<List<DiveOperation>> ListAsync(DateTime? from = null, DateTime? to = null, OperationStatus? status = null, string search = "")
    {
        Validation.Require(!from.HasValue || !to.HasValue || from.Value.Date <= to.Value.Date, "The end date must be on or after the start date.");
        await using var db = await factory.CreateDbContextAsync();
        var query = WithDetails(db).AsNoTracking();
        if (from.HasValue) query = query.Where(x => x.Date >= from.Value.Date);
        if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date) { var end = to.Value.Date.AddDays(1); query = query.Where(x => x.Date < end); }
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Id.ToString().Contains(text) || x.DiveSite!.Name.ToLower().Contains(text)
                || x.LeadInstructor != null && (x.LeadInstructor.FirstName + " " + x.LeadInstructor.LastName).ToLower().Contains(text)
                || x.Boat != null && x.Boat.Name.ToLower().Contains(text) || x.Notes != null && x.Notes.ToLower().Contains(text)
                || x.Reservation != null && x.Reservation.ReservationNumber.ToLower().Contains(text)
                || x.Participants.Any(p => (p.FirstName + " " + p.LastName).ToLower().Contains(text)));
        }
        return (await query.Include(x => x.Participants).ToListAsync()).OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).ToList();
    }
    public async Task<DiveOperation> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db).AsNoTracking()
            .Include(x => x.Participants.OrderBy(p => p.Id)).ThenInclude(x => x.Instructor)
            .Include(x => x.Participants).ThenInclude(x => x.Customer)
            .Include(x => x.Participants).ThenInclude(x => x.ReservationParticipant)
            .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Dive operation no longer exists. Refresh the list.");
    }
    public async Task<DiveOperation> DraftFromReservationAsync(int reservationId)
    {
        var reservation = await reservations.GetAsync(reservationId);
        EnsureEligible(reservation);
        return new DiveOperation
        {
            ReservationId = reservation.Id, Date = reservation.ActivityDate, StartTime = reservation.StartTime,
            DiveSiteId = reservation.DiveSiteId ?? 0, BoatId = reservation.BoatId, LeadInstructorId = reservation.InstructorId,
            DiveType = reservation.Activity!.ActivityType, Notes = reservation.Notes,
            Reservation = reservation, DiveSite = reservation.DiveSite, Boat = reservation.Boat, LeadInstructor = reservation.Instructor,
            Participants = reservation.Participants.Select(x => new DiveOperationParticipant
            {
                ReservationParticipantId = x.Id, CustomerId = x.CustomerId, FirstName = x.FirstName, LastName = x.LastName,
                InstructorId = reservation.InstructorId, Notes = x.Notes,
                Customer = x.Customer, ReservationParticipant = x, Instructor = reservation.Instructor,
                Role = reservation.Activity.ActivityType == ActivityType.TryDive ? ParticipantRole.DSDParticipant : reservation.Activity.ActivityType is ActivityType.Course or ActivityType.Specialty or ActivityType.TrainingDive ? ParticipantRole.Student : ParticipantRole.Diver
            }).ToList()
        };
    }
    private static void EnsureEligible(Reservation reservation)
    {
        Validation.Require(CanCreateFromReservation(reservation), "Only pending or confirmed diving/training reservations can create dive operations.");
    }
    public static bool CanCreateFromReservation(Reservation reservation) =>
        reservation.Status is ReservationStatus.Pending or ReservationStatus.Confirmed
        && reservation.Activity != null && Enum.IsDefined(reservation.Activity.ActivityType)
        && reservation.Activity.ActivityType is not ActivityType.EquipmentRental and not ActivityType.Other;
    public async Task SaveAsync(DiveOperation input)
    {
        input.Date = input.Date.Date;
        Validation.Check(input);
        Validation.Require(input.DiveType is not ActivityType.EquipmentRental and not ActivityType.Other, "Select a diving or training activity type for a dive operation.");
        Validation.Require(input.Participants.Count > 0, "Add at least one operation participant.");
        Validation.Require(input.Id >= 0 && input.Participants.All(x => x.Id >= 0), "Invalid operation or participant identifier.");
        var persistedIds = input.Participants.Where(x => x.Id != 0).Select(x => x.Id).ToList();
        Validation.Require(persistedIds.Distinct().Count() == persistedIds.Count
            && input.Participants.Distinct(ReferenceEqualityComparer.Instance).Count() == input.Participants.Count, "The same operation participant cannot be submitted twice.");
        var sourceIds = input.Participants.Where(x => x.ReservationParticipantId.HasValue).Select(x => x.ReservationParticipantId).ToList();
        Validation.Require(sourceIds.Distinct().Count() == sourceIds.Count, "A reservation participant can be copied only once into the same operation.");
        foreach (var person in input.Participants) Validation.Check(person);
        OperationalRules.UniqueCustomers(input.Participants.Select(x => x.CustomerId));
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var saved = input.Id == 0 ? new DiveOperation() : await db.DiveOperations.Include(x => x.Participants).SingleOrDefaultAsync(x => x.Id == input.Id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Dive operation no longer exists. Refresh the list.");
        Validation.Require(input.Status == saved.Status, "Use Start, Complete, or Cancel to change an operation's status.");
        Validation.Require(saved.Status is OperationStatus.Planned or OperationStatus.InProgress, "Completed or cancelled operations cannot be edited. Dive measurements can be edited in the logbook.");
        if (input.ReservationId.HasValue && input.Id == 0)
        {
            var reservation = await db.Reservations.Include(x => x.Activity).SingleOrDefaultAsync(x => x.Id == input.ReservationId)
                ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Source reservation no longer exists. Refresh the list.");
            EnsureEligible(reservation);
        }
        Validation.Require(input.Id == 0 || input.ReservationId == saved.ReservationId, "An operation's source reservation cannot be changed.");
        await OperationalRules.ActiveReferenceAsync<DiveSite>(db, input.DiveSiteId, saved.DiveSiteId);
        await OperationalRules.ActiveReferenceAsync<Boat>(db, input.BoatId, saved.BoatId);
        await OperationalRules.InstructorAsync(db, input.LeadInstructorId, saved.LeadInstructorId);
        await OperationalRules.BoatCapacityAsync(db, input.BoatId, input.Participants.Count);
        var created = saved.CreatedAt;
        db.Entry(saved).CurrentValues.SetValues(input); saved.CreatedAt = created; saved.UpdatedAt = input.Id == 0 ? null : DateTime.UtcNow;
        if (input.Id == 0) db.DiveOperations.Add(saved);
        db.DiveOperationParticipants.RemoveRange(saved.Participants.Where(x => input.Participants.All(p => p.Id != x.Id)).ToList());
        var savedParticipants = new List<(DiveOperationParticipant Input, DiveOperationParticipant Saved)>();
        foreach (var person in input.Participants)
        {
            var old = person.Id == 0 ? null : saved.Participants.SingleOrDefault(x => x.Id == person.Id);
            Validation.Require(person.Id == 0 || old != null, "The participant does not belong to this operation.");
            Validation.Require(old == null || person.ReservationParticipantId == old.ReservationParticipantId, "An existing participant's source reservation link cannot be changed. Remove it and add a new participant if needed.");
            await OperationalRules.ActiveReferenceAsync<Customer>(db, person.CustomerId, old?.CustomerId);
            await OperationalRules.InstructorAsync(db, person.InstructorId, old?.InstructorId);
            if (person.ReservationParticipantId.HasValue)
                Validation.Require(await db.ReservationParticipants.AnyAsync(x => x.Id == person.ReservationParticipantId && x.ReservationId == input.ReservationId), "The source participant must belong to the linked reservation.");
            if (old == null) { old = new DiveOperationParticipant(); saved.Participants.Add(old); }
            db.Entry(old).CurrentValues.SetValues(person); old.DiveOperation = saved;
            savedParticipants.Add((person, old));
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync(); input.Id = saved.Id;
        input.CreatedAt = saved.CreatedAt; input.UpdatedAt = saved.UpdatedAt;
        foreach (var (person, persisted) in savedParticipants)
        { person.Id = persisted.Id; person.DiveOperationId = saved.Id; }
    }
    public async Task ChangeStatusAsync(int id, OperationStatus next)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var operation = await db.DiveOperations.Include(x => x.Participants).Include(x => x.Reservation).SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Dive operation no longer exists. Refresh the list.");
        var allowed = operation.Status switch
        {
            OperationStatus.Planned => next is OperationStatus.InProgress or OperationStatus.Cancelled,
            OperationStatus.InProgress => next is OperationStatus.Completed or OperationStatus.Cancelled,
            _ => false
        };
        Validation.Require(allowed, $"Cannot change an operation from {operation.Status} to {next}.");
        if (next is OperationStatus.InProgress or OperationStatus.Completed)
            Validation.Require(operation.Participants.Count > 0, "Add at least one participant before starting or completing the operation.");
        if (next == OperationStatus.InProgress)
        {
            Validation.Require(operation.Reservation?.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow, "A cancelled or no-show reservation cannot start a dive operation.");
            await OperationalRules.BoatCapacityAsync(db, operation.BoatId, operation.Participants.Count);
        }
        operation.Status = next; operation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
