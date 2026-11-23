using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class EquipmentService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<Equipment>
{
    public async Task<List<Equipment>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Equipment.AsNoTracking().OrderBy(x => x.EquipmentType).ThenBy(x => x.SerialNumber).ToListAsync();
    }

    public async Task SaveAsync(Equipment record)
    {
        if (record.Status == EquipmentStatus.Retired) record.IsActive = false;
        if (!record.IsActive) record.Status = EquipmentStatus.Retired;
        await using var db = await factory.CreateDbContextAsync();
        await CatalogPersistence.SaveAsync(db, record);
    }

    // Reinstated equipment returns to Maintenance; staff can explicitly mark it Available.
    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<Equipment>(factory, id, active);
}
