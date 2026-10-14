using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class ActivityService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<Activity>
{
    public async Task<List<Activity>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Activities.AsNoTracking().Include(x => x.DefaultCurrency).OrderBy(x => x.Name).ToListAsync();
    }

    public async Task SaveAsync(Activity record)
    {
        Validation.Check(record);
        await using var db = await factory.CreateDbContextAsync();
        var previous = await db.Activities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == record.Id);
        await OperationalRules.ActiveReferenceAsync<Currency>(db, record.DefaultCurrencyId, previous?.DefaultCurrencyId);
        await CatalogPersistence.SaveAsync(db, record);
    }

    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<Activity>(factory, id, active);
}
