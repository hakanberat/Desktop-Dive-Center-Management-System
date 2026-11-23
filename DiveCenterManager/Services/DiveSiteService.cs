using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class DiveSiteService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<DiveSite>
{
    public async Task<List<DiveSite>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.DiveSites.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
    }

    public async Task SaveAsync(DiveSite record)
    {
        await using var db = await factory.CreateDbContextAsync();
        await CatalogPersistence.SaveAsync(db, record);
    }

    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<DiveSite>(factory, id, active);
}
