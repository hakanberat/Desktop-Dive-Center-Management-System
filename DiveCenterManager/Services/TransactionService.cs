using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public record TransactionFilter(int? Year = null, int? Month = null, TransactionType? Type = null, int? CurrencyId = null, int? CategoryId = null, int? ActivityId = null, string Search = "", DateTime? From = null, DateTime? To = null, int? ReservationId = null);
public record CurrencyBalance(string Code, string Symbol, decimal Income, decimal Expense)
{
    public decimal Balance => Income - Expense;
}
public record TransactionSnapshot(List<Transaction> Transactions, List<CurrencyBalance> Balances);

public sealed class TransactionService(IDbContextFactory<DiveCenterDbContext> factory)
{
    private static IQueryable<Transaction> FilterQuery(DiveCenterDbContext db, TransactionFilter filter)
    {
        Validation.Require(!filter.From.HasValue || !filter.To.HasValue || filter.From.Value.Date <= filter.To.Value.Date, "The end date must be on or after the start date.");
        Validation.Require(!filter.Year.HasValue || filter.Year is >= 1 and <= 9999, "Enter a valid year.");
        Validation.Require(!filter.Month.HasValue || filter.Month is >= 1 and <= 12, "Enter a month between 1 and 12.");
        Validation.Require(!filter.Type.HasValue || Enum.IsDefined(filter.Type.Value), "Select a valid transaction type.");
        var query = db.Transactions.AsNoTracking().Include(x => x.Activity).Include(x => x.Category).Include(x => x.Currency).Include(x => x.Reservation).AsQueryable();
        if (filter.Year.HasValue) query = query.Where(x => x.TransactionDate.Year == filter.Year);
        if (filter.Month.HasValue) query = query.Where(x => x.TransactionDate.Month == filter.Month);
        if (filter.Type.HasValue) query = query.Where(x => x.TransactionType == filter.Type);
        if (filter.CurrencyId.HasValue) query = query.Where(x => x.CurrencyId == filter.CurrencyId);
        if (filter.CategoryId.HasValue) query = query.Where(x => x.CategoryId == filter.CategoryId);
        if (filter.ActivityId.HasValue) query = query.Where(x => x.ActivityId == filter.ActivityId);
        if (filter.ReservationId.HasValue) query = query.Where(x => x.ReservationId == filter.ReservationId);
        if (filter.From.HasValue) query = query.Where(x => x.TransactionDate >= filter.From.Value.Date);
        if (filter.To.HasValue && filter.To.Value.Date < DateTime.MaxValue.Date) { var end = filter.To.Value.Date.AddDays(1); query = query.Where(x => x.TransactionDate < end); }
        return query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id);
    }

    private static async Task<List<Transaction>> ReadAsync(DiveCenterDbContext db, TransactionFilter filter)
    {
        var rows = await FilterQuery(db, filter).ToListAsync();
        if (string.IsNullOrWhiteSpace(filter.Search)) return rows;
        var search = filter.Search.Trim();
        // SQLite lower() is ASCII-only. Apply Unicode-aware text matching after the structured SQL filters.
        bool Matches(string? value) => value != null && (value.Contains(search, StringComparison.OrdinalIgnoreCase) || value.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        return rows.Where(x => Matches(x.Description) || Matches(x.Category?.Name) || Matches(x.Activity?.Name)).ToList();
    }

    public async Task<List<Transaction>> ListAsync(TransactionFilter filter)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await ReadAsync(db, filter);
    }

    public async Task<Transaction> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Transactions.AsNoTracking().Include(x => x.Activity).Include(x => x.Category).Include(x => x.Currency).Include(x => x.Reservation)
            .SingleOrDefaultAsync(x => x.Id == id) ?? throw new System.ComponentModel.DataAnnotations.ValidationException("This cash transaction no longer exists. Refresh the list.");
    }

    public async Task<TransactionSnapshot> GetSnapshotAsync(TransactionFilter filter)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var snapshot = await db.Database.BeginTransactionAsync();
        var transactions = await ReadAsync(db, filter);
        var usedIds = transactions.Select(x => x.CurrencyId).Distinct().ToList();
        var currencies = await db.Currencies.AsNoTracking().Where(x => x.IsActive || usedIds.Contains(x.Id) || x.Id == filter.CurrencyId).OrderBy(x => x.Code).ToListAsync();
        var balances = currencies.Where(x => !filter.CurrencyId.HasValue || x.Id == filter.CurrencyId).Select(currency =>
        {
            var rows = transactions.Where(x => x.CurrencyId == currency.Id).ToList();
            return new CurrencyBalance(currency.Code, currency.Symbol, rows.Where(x => x.TransactionType == TransactionType.Income).Sum(x => x.Amount), rows.Where(x => x.TransactionType == TransactionType.Expense).Sum(x => x.Amount));
        }).ToList();
        await snapshot.CommitAsync();
        return new TransactionSnapshot(transactions, balances);
    }
    public async Task<List<CurrencyBalance>> BalancesAsync(TransactionFilter filter) => (await GetSnapshotAsync(filter)).Balances;
    public async Task SaveAsync(Transaction input)
    {
        input.TransactionDate = input.TransactionDate.Date;
        Validation.Check(input);
        Validation.Require(input.Id >= 0, "Invalid transaction identifier.");
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var saved = input.Id == 0 ? new Transaction() : await db.Transactions.SingleOrDefaultAsync(x => x.Id == input.Id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("This cash transaction no longer exists. Refresh the list.");
        await OperationalRules.ActiveReferenceAsync<Currency>(db, input.CurrencyId, saved.CurrencyId);
        await OperationalRules.ActiveReferenceAsync<Activity>(db, input.ActivityId, saved.ActivityId);
        await OperationalRules.ActiveReferenceAsync<TransactionCategory>(db, input.CategoryId, saved.CategoryId);
        if (input.CategoryId.HasValue)
        {
            var category = await db.TransactionCategories.SingleAsync(x => x.Id == input.CategoryId);
            Validation.Require(category.Type == CategoryType.Both || category.Type.ToString() == input.TransactionType.ToString(), "The category does not support this transaction type.");
        }
        if (input.ReservationId.HasValue) Validation.Require(await db.Reservations.AnyAsync(x => x.Id == input.ReservationId), "Select an existing reservation.");
        var created = saved.CreatedAt;
        db.Entry(saved).CurrentValues.SetValues(input); saved.CreatedAt = created; saved.UpdatedAt = input.Id == 0 ? null : DateTime.UtcNow;
        if (input.Id == 0) db.Transactions.Add(saved);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        input.Id = saved.Id; input.CreatedAt = saved.CreatedAt; input.UpdatedAt = saved.UpdatedAt;
    }
    public async Task DeleteAsync(int id, bool confirmed)
    {
        Validation.Require(confirmed, "Explicit confirmation is required to delete a transaction.");
        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.Transactions.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("This cash transaction no longer exists. Refresh the list.");
        db.Transactions.Remove(saved); await db.SaveChangesAsync();
    }
}
