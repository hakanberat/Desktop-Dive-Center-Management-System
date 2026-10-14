using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public record Choice(object? Value, string Label) { public override string ToString() => Label; }

public sealed class LookupService(MasterDataService masters, IDbContextFactory<DiveCenterDbContext> factory)
{
    public async Task<List<Choice>> GetAsync(string propertyName, object? current = null, bool includeInactive = false)
    {
        if (propertyName == "ReservationId")
        {
            await using var db = await factory.CreateDbContextAsync();
            return (await db.Reservations.AsNoTracking().Include(x => x.Activity).OrderByDescending(x => x.ActivityDate).ToListAsync())
                .Select(x => new Choice(x.Id, x.ToString())).ToList();
        }
        var type = propertyName switch
        {
            "CustomerId" or "MainCustomerId" or "BuddyCustomerId" => typeof(Customer),
            "ActivityId" => typeof(Activity),
            "DiveSiteId" => typeof(DiveSite),
            "BoatId" => typeof(Boat),
            "CurrencyId" or "DefaultCurrencyId" => typeof(Currency),
            "CategoryId" => typeof(TransactionCategory),
            "InstructorId" or "LeadInstructorId" => typeof(Staff),
            _ => throw new ArgumentException("Unknown lookup: " + propertyName)
        };
        var values = (await masters.ListAsync(type)).Cast<IActiveEntity>().Where(x => includeInactive || x.IsActive || Equals(current, x.Id));
        if (type == typeof(Staff)) values = values.Cast<Staff>().Where(x => x.StaffRoles.Any(r => r.StaffRole?.Name == "Instructor" && r.StaffRole.IsActive) || Equals(current, x.Id));
        return values.Select(x => new Choice(x.Id, x + (x.IsActive ? "" : " (inactive)"))).ToList();
    }
}
