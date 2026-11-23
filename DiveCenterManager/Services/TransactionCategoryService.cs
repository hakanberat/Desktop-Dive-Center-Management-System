using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public sealed class TransactionCategoryService(IDbContextFactory<DiveCenterDbContext> factory) : ICatalogService<TransactionCategory>
{
    public async Task<List<TransactionCategory>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TransactionCategories.AsNoTracking().OrderBy(x => x.Type).ThenBy(x => x.Name).ToListAsync();
    }

    public async Task SaveAsync(TransactionCategory record)
    {
        Validation.Check(record);
        await using var db = await factory.CreateDbContextAsync();
        if (record.Type != CategoryType.Both)
        {
            var incompatible = record.Type == CategoryType.Income ? TransactionType.Expense : TransactionType.Income;
            Validation.Require(!await db.Transactions.AnyAsync(x => x.CategoryId == record.Id && x.TransactionType == incompatible),
                "This category has transactions of the other type. Keep Both or create a separate category.");
        }
        await CatalogPersistence.SaveAsync(db, record);
    }

    public Task SetActiveAsync(int id, bool active) => CatalogPersistence.SetActiveAsync<TransactionCategory>(factory, id, active);
}
