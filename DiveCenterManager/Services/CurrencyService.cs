using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class CurrencyService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<Currency>
{
    public async Task<List<Currency>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Currencies.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
    }

    public async Task SaveAsync(Currency record)
    {
        record.Code = record.Code.Trim().ToUpperInvariant();
        Validation.Check(record);
        await using var db = await factory.CreateDbContextAsync();
        Validation.Require(!await db.Currencies.AnyAsync(x => x.Code == record.Code && x.Id != record.Id), "A currency with this code already exists.");
        var previous = await db.Currencies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == record.Id);
        if (previous != null && previous.Code != record.Code)
            Validation.Require(!await db.Transactions.AnyAsync(x => x.CurrencyId == record.Id), "The code of a currency with cash history cannot be changed. Add a separate currency instead.");
        await CatalogPersistence.SaveAsync(db, record);
    }

    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<Currency>(factory, id, active);
}
