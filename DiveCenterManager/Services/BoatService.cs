using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class BoatService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<Boat>
{
    public async Task<List<Boat>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Boats.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
    }

    public async Task SaveAsync(Boat record)
    {
        await using var db = await factory.CreateDbContextAsync();
        await CatalogPersistence.SaveAsync(db, record);
    }

    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<Boat>(factory, id, active);
}
