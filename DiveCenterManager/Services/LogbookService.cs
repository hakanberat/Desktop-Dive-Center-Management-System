using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class LogbookService(IDbContextFactory<DiveCenterDbContext> factory, DiveOperationService operations)
{
    public async Task<List<DiveLog>> ListAsync(DateTime? from = null, DateTime? to = null, int? siteId = null)
    {
        Validation.Require(!from.HasValue || !to.HasValue || from.Value.Date <= to.Value.Date, "The end date must be on or after the start date.");
        await using var db = await factory.CreateDbContextAsync();
        var query = db.DiveLogs.AsNoTracking().Include(x => x.DiveSite).Include(x => x.DiveOperation).Include(x => x.DiverLogs).AsQueryable();
        if (from.HasValue) query = query.Where(x => x.Date >= from.Value.Date);
        if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date) { var end = to.Value.Date.AddDays(1); query = query.Where(x => x.Date < end); }
        if (siteId.HasValue) query = query.Where(x => x.DiveSiteId == siteId);
        return (await query.ToListAsync()).OrderByDescending(x => x.Date).ThenByDescending(x => x.EntryTime).ToList();
    }
    public async Task<DiveLog> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.DiveLogs.AsNoTracking().Include(x => x.DiveSite)
            .Include(x => x.DiveOperation).ThenInclude(x => x!.Reservation)
            .Include(x => x.DiverLogs).ThenInclude(x => x.Customer)
            .Include(x => x.DiverLogs).ThenInclude(x => x.Instructor)
            .Include(x => x.DiverLogs).ThenInclude(x => x.BuddyCustomer)
            .SingleOrDefaultAsync(x => x.Id == id) ?? throw new System.ComponentModel.DataAnnotations.ValidationException("This dive log no longer exists. Refresh the logbook.");
    }
    public async Task<DiveLog> DraftFromOperationAsync(int operationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.DiveLogs.AsNoTracking().SingleOrDefaultAsync(x => x.DiveOperationId == operationId);
        if (existing != null) return await GetAsync(existing.Id);
        var operation = await operations.GetAsync(operationId);
        Validation.Require(operation.Status == OperationStatus.Completed, "Complete the dive operation before creating its log.");
        Validation.Require(operation.Participants.Count > 0, "The completed operation must contain participants.");
        return new DiveLog
        {
            DiveOperationId = operation.Id, DiveSiteId = operation.DiveSiteId, Date = operation.Date,
            EntryTime = operation.StartTime, ExitTime = operation.StartTime,
            BottomTimeMinutes = 0, DiveType = operation.DiveType,
            DiverLogs = operation.Participants.Select(x => new DiverDiveLog
            {
                CustomerId = x.CustomerId, ParticipantName = x.FullName, InstructorId = x.InstructorId ?? operation.LeadInstructorId, GasType = GasType.Air
            }).ToList()
        };
    }
    public async Task SaveAsync(DiveLog input)
    {
        input.Date = input.Date.Date;
        Validation.Check(input);
        Validation.Require(input.DiverLogs.Count > 0, "Add at least one personal diver log.");
        foreach (var person in input.DiverLogs) Validation.Check(person);
        OperationalRules.UniqueCustomers(input.DiverLogs.Select(x => x.CustomerId));
        Validation.Require(input.DiverLogs.Distinct(ReferenceEqualityComparer.Instance).Count() == input.DiverLogs.Count
            && input.DiverLogs.Where(x => x.Id != 0).Select(x => x.Id).Distinct().Count() == input.DiverLogs.Count(x => x.Id != 0),
            "The same personal log cannot be included twice.");
        Validation.Require(input.DiverLogs.All(x => x.Id >= 0 && (x.DiveLogId == 0 || x.DiveLogId == input.Id)), "The personal log does not belong to this dive.");
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        Validation.Require(await db.DiveOperations.AnyAsync(x => x.Id == input.DiveOperationId && x.Status == OperationStatus.Completed), "A dive log must belong to a completed dive operation.");
        Validation.Require(!await db.DiveLogs.AnyAsync(x => x.DiveOperationId == input.DiveOperationId && x.Id != input.Id), "This operation already has a dive log. Open the existing log to edit it.");
        var saved = input.Id == 0 ? new DiveLog() : await db.DiveLogs.Include(x => x.DiverLogs).SingleAsync(x => x.Id == input.Id);
        Validation.Require(input.Id == 0 || saved.DiveOperationId == input.DiveOperationId, "A log's dive operation cannot be changed.");
        await OperationalRules.ActiveReferenceAsync<DiveSite>(db, input.DiveSiteId, saved.DiveSiteId);
        var oldCustomerIds = saved.DiverLogs.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).ToList();
        var newCustomerIds = input.DiverLogs.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).ToList();
        var affectedIds = oldCustomerIds.Union(newCustomerIds).ToList();
        db.Entry(saved).CurrentValues.SetValues(input);
        if (input.Id == 0) db.DiveLogs.Add(saved);
        foreach (var removed in saved.DiverLogs.Where(x => input.DiverLogs.All(p => p.Id != x.Id)).ToList())
        { db.DiverDiveLogs.Remove(removed); saved.DiverLogs.Remove(removed); }
        var identities = new List<(DiverDiveLog Input, DiverDiveLog Saved)>();
        foreach (var person in input.DiverLogs)
        {
            var old = person.Id == 0 ? null : saved.DiverLogs.SingleOrDefault(x => x.Id == person.Id);
            Validation.Require(person.Id == 0 || old != null, "The personal log does not belong to this dive.");
            await ValidatePersonReferencesAsync(db, person, old);
            if (old == null) { old = new DiverDiveLog(); saved.DiverLogs.Add(old); }
            db.Entry(old).CurrentValues.SetValues(person); old.DiveLog = saved;
            identities.Add((person, old));
        }
        await db.SaveChangesAsync();
        await UpdateCustomerHistoryAsync(db, affectedIds);
        await db.SaveChangesAsync(); await transaction.CommitAsync(); input.Id = saved.Id;
        foreach (var pair in identities) { pair.Input.Id = pair.Saved.Id; pair.Input.DiveLogId = saved.Id; }
    }

    // Save only the selected diver, so editing personal data cannot overwrite shared conditions or other divers.
    public async Task SavePersonalAsync(DiverDiveLog input)
    {
        Validation.Check(input);
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var saved = await db.DiverDiveLogs.SingleOrDefaultAsync(x => x.Id == input.Id);
        Validation.Require(saved != null && saved.DiveLogId == input.DiveLogId, "The personal log does not belong to this dive. Reopen its details.");
        await ValidatePersonReferencesAsync(db, input, saved);
        Validation.Require(!input.CustomerId.HasValue || !await db.DiverDiveLogs.AnyAsync(x => x.DiveLogId == input.DiveLogId && x.CustomerId == input.CustomerId && x.Id != input.Id),
            "This customer already has a personal log for this dive.");
        var affectedIds = new[] { saved!.CustomerId, input.CustomerId }.OfType<int>().Distinct().ToList();
        db.Entry(saved).CurrentValues.SetValues(input);
        await db.SaveChangesAsync();
        await UpdateCustomerHistoryAsync(db, affectedIds);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }

    public async Task SetVerifiedAsync(int logId, int personalLogId, bool verified)
    {
        await using var db = await factory.CreateDbContextAsync();
        var person = await db.DiverDiveLogs.SingleOrDefaultAsync(x => x.Id == personalLogId && x.DiveLogId == logId);
        Validation.Require(person != null, "The personal log no longer exists. Refresh the dive details.");
        person!.IsVerified = verified;
        await db.SaveChangesAsync();
    }

    private static async Task ValidatePersonReferencesAsync(DiveCenterDbContext db, DiverDiveLog person, DiverDiveLog? old)
    {
        await OperationalRules.ActiveReferenceAsync<Customer>(db, person.CustomerId, old?.CustomerId);
        await OperationalRules.ActiveReferenceAsync<Customer>(db, person.BuddyCustomerId, old?.BuddyCustomerId);
        Validation.Require(!person.CustomerId.HasValue || person.CustomerId != person.BuddyCustomerId, "A diver cannot be their own buddy.");
        await OperationalRules.InstructorAsync(db, person.InstructorId, old?.InstructorId);
    }

    private static async Task UpdateCustomerHistoryAsync(DiveCenterDbContext db, List<int> affectedIds)
    {
        foreach (var customer in await db.Customers.Where(x => affectedIds.Contains(x.Id)).ToListAsync())
        {
            customer.TotalLoggedDives = customer.PriorLoggedDives + await db.DiverDiveLogs.CountAsync(x => x.CustomerId == customer.Id);
            var last = await db.DiverDiveLogs.Where(x => x.CustomerId == customer.Id).Select(x => (DateTime?)x.DiveLog!.Date).MaxAsync();
            customer.LastDiveDate = last.HasValue && (!customer.PriorLastDiveDate.HasValue || last > customer.PriorLastDiveDate) ? last : customer.PriorLastDiveDate;
            customer.UpdatedAt = DateTime.UtcNow;
        }
    }
}
