using DiveCenterManager.Models;
using DiveCenterManager.Helpers;
using DiveCenterManager.Services;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Data;

public sealed class DatabaseInitializer(IDbContextFactory<DiveCenterDbContext> factory, AppPaths paths, BackupService backups)
{
    public async Task InitializeAsync()
    {
        paths.ValidateStorage();
        await using var db = await factory.CreateDbContextAsync();
        var existed = File.Exists(paths.DatabasePath) && new FileInfo(paths.DatabasePath).Length > 0;
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (existed && pending.Count > 0) await backups.CreateAsync(Path.Combine(paths.BackupDirectory, $"before-migration-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db"));
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.Currencies.AnyAsync()) db.Currencies.AddRange(
            new Currency { Code = "TRY", Name = "Turkish Lira", Symbol = "₺" },
            new Currency { Code = "EUR", Name = "Euro", Symbol = "€" },
            new Currency { Code = "USD", Name = "US Dollar", Symbol = "$" },
            new Currency { Code = "GBP", Name = "British Pound", Symbol = "£" });
        if (!await db.StaffRoles.AnyAsync())
            db.StaffRoles.AddRange(new[] { "Instructor", "Divemaster", "Captain", "Staff", "Owner", "LifeGuard" }.Select(name => new StaffRole { Name = name }));
        if (!await db.TransactionCategories.AnyAsync())
        {
            db.TransactionCategories.AddRange(new[] { "Diving", "Course", "Equipment Rental", "Other Income" }.Select(name => new TransactionCategory { Name = name, Type = CategoryType.Income }));
            db.TransactionCategories.AddRange(new[] { "Fuel", "Vehicle", "Boat", "Equipment", "Compressor", "PADI", "PIC", "Tax", "Office", "Website", "Advertising", "Food", "Maintenance", "Transportation", "Rent", "Salary", "Other Expense" }.Select(name => new TransactionCategory { Name = name, Type = CategoryType.Expense }));
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
