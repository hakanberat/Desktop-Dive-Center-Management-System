using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

// EF tracking helpers for catalog services. Queries and business rules stay in each service.
internal static class CatalogPersistence
{
    public static async Task SaveAsync<T>(DiveCenterDbContext db, T record) where T : class, IActiveEntity
    {
        Validation.Check(record);
        Validation.Require(record.Id >= 0, "Invalid record identifier.");
        if (record.Id == 0) db.Entry(record).State = EntityState.Added;
        else
        {
            var saved = await db.Set<T>().FindAsync(record.Id)
                ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Record no longer exists. Refresh the list.");
            db.Entry(saved).CurrentValues.SetValues(record);
        }
        await db.SaveChangesAsync();
    }

    public static async Task SetActiveAsync<T>(IDbContextFactory<DiveCenterDbContext> factory, int id, bool active)
        where T : class, IActiveEntity
    {
        await using var db = await factory.CreateDbContextAsync();
        var record = await db.Set<T>().FindAsync(id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Record no longer exists. Refresh the list.");
        record.IsActive = active;
        if (record is Customer customer) customer.UpdatedAt = DateTime.UtcNow;
        if (record is Equipment equipment)
        {
            if (!active) equipment.Status = EquipmentStatus.Retired;
            else if (equipment.Status == EquipmentStatus.Retired) equipment.Status = EquipmentStatus.Maintenance;
        }
        await db.SaveChangesAsync();
    }
}
