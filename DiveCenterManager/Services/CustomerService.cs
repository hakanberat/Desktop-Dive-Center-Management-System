using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public record CustomerDiveRow(int LogId, DateTime Date, string Site, string Instructor, string Buddy, decimal MaxDepthMeters, int BottomTimeMinutes, GasType GasType, bool Verified, string? Notes);
public record CustomerHistory(Customer Customer, List<Reservation> Reservations, List<CustomerDiveRow> Dives);

public sealed class CustomerService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<Customer>
{
    public async Task<List<Customer>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Customers.AsNoTracking().OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToListAsync();
    }

    public async Task SaveAsync(Customer record)
    {
        Validation.Check(record);
        await using var db = await factory.CreateDbContextAsync();
        var previous = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == record.Id);
        var localCount = await db.DiverDiveLogs.CountAsync(x => x.CustomerId == record.Id);
        var last = await db.DiverDiveLogs.Where(x => x.CustomerId == record.Id).Select(x => (DateTime?)x.DiveLog!.Date).MaxAsync();
        Validation.Require(record.TotalLoggedDives >= localCount, $"Total logged dives cannot be less than the {localCount} dives recorded at this center.");
        record.PriorLoggedDives = record.TotalLoggedDives - localCount;
        record.PriorLastDiveDate = previous == null || record.LastDiveDate != previous.LastDiveDate ? record.LastDiveDate : previous.PriorLastDiveDate;
        record.LastDiveDate = last.HasValue && (!record.PriorLastDiveDate.HasValue || last > record.PriorLastDiveDate) ? last : record.PriorLastDiveDate;
        record.CreatedAt = previous?.CreatedAt ?? DateTime.UtcNow;
        record.UpdatedAt = previous == null ? null : DateTime.UtcNow;
        await CatalogPersistence.SaveAsync(db, record);
    }

    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<Customer>(factory, id, active);

    public async Task<CustomerHistory> HistoryAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var customer = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == id);
        var reservations = await db.Reservations.AsNoTracking().Include(x => x.Activity).Include(x => x.Instructor).Include(x => x.Boat).Include(x => x.DiveSite)
            .Where(x => x.MainCustomerId == id || x.Participants.Any(p => p.CustomerId == id)).OrderByDescending(x => x.ActivityDate).ToListAsync();
        var logs = await db.DiverDiveLogs.AsNoTracking().Include(x => x.DiveLog).ThenInclude(x => x!.DiveSite).Include(x => x.Instructor).Include(x => x.BuddyCustomer)
            .Where(x => x.CustomerId == id).OrderByDescending(x => x.DiveLog!.Date).ToListAsync();
        return new CustomerHistory(customer, reservations, logs.Select(x => new CustomerDiveRow(x.DiveLogId, x.DiveLog!.Date, x.DiveLog.DiveSite!.Name, x.Instructor?.FullName ?? "", x.BuddyCustomer?.FullName ?? "", x.MaxDepthMeters ?? x.DiveLog.MaxDepthMeters, x.BottomTimeMinutes ?? x.DiveLog.BottomTimeMinutes, x.GasType, x.IsVerified, x.PersonalNotes)).ToList());
    }
}
