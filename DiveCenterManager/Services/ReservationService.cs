using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public record ReservationFilter(DateTime? From = null, DateTime? To = null, int? ActivityId = null, ReservationStatus? Status = null, string Search = "");

public sealed class ReservationService(IDbContextFactory<DiveCenterDbContext> factory)
{
    private static IQueryable<Reservation> WithDetails(DiveCenterDbContext db) => db.Reservations
        .Include(x => x.Activity).Include(x => x.MainCustomer).Include(x => x.Instructor).Include(x => x.Boat).Include(x => x.DiveSite);
    public async Task<List<Reservation>> ListAsync(ReservationFilter filter)
    {
        Validation.Require(!filter.From.HasValue || !filter.To.HasValue || filter.From.Value.Date <= filter.To.Value.Date, "The end date must be on or after the start date.");
        await using var db = await factory.CreateDbContextAsync();
        var query = WithDetails(db).AsNoTracking();
        if (filter.From.HasValue) query = query.Where(x => x.ActivityDate >= filter.From.Value.Date);
        if (filter.To.HasValue && filter.To.Value.Date < DateTime.MaxValue.Date) { var end = filter.To.Value.Date.AddDays(1); query = query.Where(x => x.ActivityDate < end); }
        if (filter.ActivityId.HasValue) query = query.Where(x => x.ActivityId == filter.ActivityId);
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var text = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.ReservationNumber.ToLower().Contains(text) || x.Activity!.Name.ToLower().Contains(text)
                || (x.Notes != null && x.Notes.ToLower().Contains(text))
                || x.Participants.Any(p => (p.FirstName + " " + p.LastName).ToLower().Contains(text))
                || x.MainCustomer != null && (x.MainCustomer.FirstName + " " + x.MainCustomer.LastName).ToLower().Contains(text));
        }
        return (await query.ToListAsync()).OrderBy(x => x.ActivityDate).ThenBy(x => x.StartTime).ToList();
    }
    public async Task<Reservation> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await WithDetails(db).AsNoTracking().Include(x => x.Participants.OrderBy(p => p.Id)).ThenInclude(x => x.Customer).SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Reservation no longer exists. Refresh the list.");
    }
    public async Task SaveAsync(Reservation input)
    {
        input.ActivityDate = input.ActivityDate.Date; input.ReservationDate = input.ReservationDate.Date;
        Validation.Check(input);
        Validation.Require(input.Id >= 0 && input.Participants.All(x => x.Id >= 0), "Invalid reservation or participant identifier.");
        var existingIds = input.Participants.Where(x => x.Id != 0).Select(x => x.Id).ToList();
        Validation.Require(existingIds.Distinct().Count() == existingIds.Count, "The same participant row cannot be submitted twice.");
        foreach (var participant in input.Participants) Validation.Check(participant);
        OperationalRules.UniqueCustomers(input.Participants.Select(x => x.CustomerId));
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var saved = input.Id == 0 ? new Reservation() : await db.Reservations.Include(x => x.Participants).SingleOrDefaultAsync(x => x.Id == input.Id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Reservation no longer exists. Refresh the list.");
        await OperationalRules.ActiveReferenceAsync<Activity>(db, input.ActivityId, saved.ActivityId);
        await OperationalRules.ActiveReferenceAsync<Customer>(db, input.MainCustomerId, saved.MainCustomerId);
        await OperationalRules.ActiveReferenceAsync<DiveSite>(db, input.DiveSiteId, saved.DiveSiteId);
        await OperationalRules.ActiveReferenceAsync<Boat>(db, input.BoatId, saved.BoatId);
        await OperationalRules.InstructorAsync(db, input.InstructorId, saved.InstructorId);
        await OperationalRules.BoatCapacityAsync(db, input.BoatId, input.NumberOfParticipants);
        if (input.Status is ReservationStatus.Cancelled or ReservationStatus.NoShow)
            await CancelOpenOperationsAsync(db, input.Id);
        var number = saved.ReservationNumber;
        var created = saved.CreatedAt;
        db.Entry(saved).CurrentValues.SetValues(input);
        saved.CreatedAt = created;
        saved.UpdatedAt = input.Id == 0 ? null : DateTime.UtcNow;
        if (input.Id == 0)
        {
            var year = input.ReservationDate.Year;
            var sequence = await db.NumberSequences.SingleOrDefaultAsync(x => x.Year == year);
            if (sequence == null) { sequence = new NumberSequence { Year = year }; db.NumberSequences.Add(sequence); }
            Validation.Require(sequence.LastValue is >= 0 and < 999999, "The annual reservation number range is exhausted or invalid.");
            sequence.LastValue++;
            saved.ReservationNumber = FormattableString.Invariant($"RES-{year:D4}-{sequence.LastValue:D6}");
            db.Reservations.Add(saved);
        }
        else saved.ReservationNumber = number;
        var removed = saved.Participants.Where(x => input.Participants.All(p => p.Id != x.Id)).ToList();
        var removedIds = removed.Select(x => x.Id).ToList();
        Validation.Require(!await db.DiveOperationParticipants.AnyAsync(x => x.ReservationParticipantId.HasValue && removedIds.Contains(x.ReservationParticipantId.Value)), "A participant already used in a dive operation cannot be removed from the reservation.");
        db.ReservationParticipants.RemoveRange(removed);
        var savedParticipants = new List<(ReservationParticipant Input, ReservationParticipant Saved)>();
        foreach (var person in input.Participants)
        {
            var old = person.Id == 0 ? null : saved.Participants.SingleOrDefault(x => x.Id == person.Id);
            Validation.Require(person.Id == 0 || old != null, "The participant does not belong to this reservation.");
            await OperationalRules.ActiveReferenceAsync<Customer>(db, person.CustomerId, old?.CustomerId);
            if (old == null) { old = new ReservationParticipant(); saved.Participants.Add(old); }
            db.Entry(old).CurrentValues.SetValues(person);
            old.Reservation = saved;
            savedParticipants.Add((person, old));
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        input.Id = saved.Id; input.ReservationNumber = saved.ReservationNumber;
        input.CreatedAt = saved.CreatedAt; input.UpdatedAt = saved.UpdatedAt;
        foreach (var (person, persisted) in savedParticipants)
        {
            person.Id = persisted.Id;
            person.ReservationId = saved.Id;
        }
    }
    public async Task CancelAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Reservation no longer exists. Refresh the list.");
        if (reservation.Status == ReservationStatus.Cancelled) return;
        await CancelOpenOperationsAsync(db, id);
        // Cancellation changes only the status; historical dates, participants and payments remain intact.
        reservation.Status = ReservationStatus.Cancelled;
        reservation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    private static async Task CancelOpenOperationsAsync(DiveCenterDbContext db, int id)
    {
        // Legacy operations are no longer managed in the UI. Closing a booking also
        // closes its unfinished operations, while completed dives and logs are retained.
        var operations = await db.DiveOperations.Where(x => x.ReservationId == id && (x.Status == OperationStatus.Planned || x.Status == OperationStatus.InProgress)).ToListAsync();
        foreach (var operation in operations)
        {
            operation.Status = OperationStatus.Cancelled;
            operation.UpdatedAt = DateTime.UtcNow;
        }
    }
    public async Task<string> PaymentSummaryAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var transactions = await db.Transactions.AsNoTracking().Include(x => x.Currency).Where(x => x.ReservationId == id).ToListAsync();
        return transactions.Count == 0 ? "No cash transactions linked to this reservation." : "Linked cash transactions\n" + string.Join("\n", transactions.GroupBy(x => x.Currency!.Code).OrderBy(x => x.Key).Select(g => $"{g.Key} · Income {g.Where(x => x.TransactionType == TransactionType.Income).Sum(x => x.Amount):#,##0.00##########################} · Expense {g.Where(x => x.TransactionType == TransactionType.Expense).Sum(x => x.Amount):#,##0.00##########################} · Net {g.Sum(x => x.TransactionType == TransactionType.Income ? x.Amount : -x.Amount):#,##0.00##########################}"));
    }
    public async Task<List<Transaction>> PaymentsAsync(int reservationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Transactions.AsNoTracking().Include(x => x.Currency).Include(x => x.Category).Include(x => x.Activity).Include(x => x.Reservation).Where(x => x.ReservationId == reservationId).OrderBy(x => x.Currency!.Code).ThenBy(x => x.TransactionDate).ToListAsync();
    }
}
