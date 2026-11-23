using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public record DashboardCash(string Code, decimal TodayIncome, decimal TodayExpense, decimal CurrentBalance);
public record UpcomingReservationRow(int Id, string ReservationNumber, DateTime Date, TimeSpan StartTime, string Activity, int Participants, ReservationStatus Status);
public record DashboardSnapshot(int TodayReservations, int PendingReservations, int Participants, int CompletedReservations, List<UpcomingReservationRow> Upcoming, List<UpcomingReservationRow> RecentReservations, List<DashboardCash> Cash);

public sealed class DashboardService(IDbContextFactory<DiveCenterDbContext> factory)
{
    public async Task<DashboardSnapshot> GetAsync(DateTime today)
    {
        today = today.Date;
        Validation.Require(today != default, "Select a valid dashboard date.");
        var nextWeekEnd = today <= DateTime.MaxValue.Date.AddDays(-6) ? today.AddDays(6) : DateTime.MaxValue.Date;
        await using var db = await factory.CreateDbContextAsync();
        await using var snapshot = await db.Database.BeginTransactionAsync();
        var activeBookings = db.Reservations.AsNoTracking().Where(x => x.Status != ReservationStatus.Cancelled && x.Status != ReservationStatus.NoShow);
        var bookingsToday = activeBookings.InDateRange(x => x.ActivityDate, today, today);
        var todayReservations = await bookingsToday.CountAsync();
        var planned = await bookingsToday.CountAsync(x => x.Status == ReservationStatus.Pending || x.Status == ReservationStatus.Confirmed);
        var completed = await bookingsToday.CountAsync(x => x.Status == ReservationStatus.Completed);
        var participants = await bookingsToday.SumAsync(x => x.NumberOfParticipants);

        var upcoming = await activeBookings.InDateRange(x => x.ActivityDate, today, nextWeekEnd)
            .Where(x => x.Status == ReservationStatus.Pending || x.Status == ReservationStatus.Confirmed)
            .OrderBy(x => x.ActivityDate).ThenBy(x => x.Id)
            .Select(x => new UpcomingReservationRow(x.Id, x.ReservationNumber, x.ActivityDate, x.StartTime, x.Activity!.Name, x.NumberOfParticipants, x.Status)).ToListAsync();
        // Only the bounded seven-day projection is sorted by TimeSpan in memory.
        upcoming = upcoming.OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).ToList();

        var recent = await activeBookings.Where(x => x.Status == ReservationStatus.Completed)
            .OrderByDescending(x => x.ActivityDate).ThenByDescending(x => x.Id)
            .Select(x => new UpcomingReservationRow(x.Id, x.ReservationNumber, x.ActivityDate, x.StartTime, x.Activity!.Name, x.NumberOfParticipants, x.Status)).Take(6).ToListAsync();

        var tomorrow = today < DateTime.MaxValue.Date ? today.AddDays(1) : DateTime.MaxValue;
        // Decimal Sum translates to ef_sum; no transaction entities or floating-point totals are loaded.
        var totals = await db.Transactions.AsNoTracking().GroupBy(x => x.CurrencyId).Select(g => new
        {
            CurrencyId = g.Key,
            Income = g.Sum(x => x.TransactionType == TransactionType.Income ? x.Amount : 0m),
            Expense = g.Sum(x => x.TransactionType == TransactionType.Expense ? x.Amount : 0m),
            TodayIncome = g.Sum(x => x.TransactionDate >= today && x.TransactionDate < tomorrow && x.TransactionType == TransactionType.Income ? x.Amount : 0m),
            TodayExpense = g.Sum(x => x.TransactionDate >= today && x.TransactionDate < tomorrow && x.TransactionType == TransactionType.Expense ? x.Amount : 0m)
        }).ToDictionaryAsync(x => x.CurrencyId);
        var currencies = await db.Currencies.AsNoTracking().Where(x => x.IsActive || db.Transactions.Any(t => t.CurrencyId == x.Id))
            .OrderBy(x => x.Code).Select(x => new { x.Id, x.Code }).ToListAsync();
        var cash = currencies.Select(currency => totals.TryGetValue(currency.Id, out var total)
            ? new DashboardCash(currency.Code, total.TodayIncome, total.TodayExpense, total.Income - total.Expense)
            : new DashboardCash(currency.Code, 0, 0, 0)).ToList();
        await snapshot.CommitAsync();
        return new DashboardSnapshot(todayReservations, planned, participants, completed, upcoming, recent, cash);
    }
}
