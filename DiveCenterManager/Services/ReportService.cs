using System.Globalization;
using DiveCenterManager.Data;
using DiveCenterManager.Models;
using Microsoft.EntityFrameworkCore;

namespace DiveCenterManager.Services;

public enum ReportKind
{
    ReservationsByDate, ReservationsByActivity, ParticipantCounts, CancelledReservations, NoShows,
    DiveOperationsByDate, DivesByDiveSite, DivesByInstructor, CustomerDiveHistory, LoggedDivesPerCustomer,
    IncomeByDate, ExpenseByDate, IncomeByCategory, ExpenseByCategory, MonthlySummary, ActivityIncomeSummary
}
public record ReportFilter(ReportKind Kind, DateTime? From, DateTime? To, int? ActivityId = null, int? CustomerId = null,
    int? DiveSiteId = null, int? InstructorId = null, int? Year = null, int? CurrencyId = null);
public record OperationalReportRow(string Group, int Reservations, int Participants, int Dives)
{
    public int? GroupId { get; init; }
}
public record FinancialReportRow(string Group, string Currency, long Transactions, long Persons, long Dives, decimal Income, decimal Expense)
{
    public decimal NetCash => Income - Expense;
    public int? GroupId { get; init; }
    public int? Year { get; init; }
    public int? Month { get; init; }
}
public record ReservationReportRow(int Id, string ReservationNumber, DateTime Date, TimeSpan StartTime, string Activity, int Participants, string MainCustomer, string Instructor, string DiveSite, string Boat, ReservationStatus Status);
public record OperationReportRow(int Id, DateTime Date, TimeSpan StartTime, string DiveSite, string Instructor, string Boat, ActivityType DiveType, int Participants, string Reservation, OperationStatus Status);
public record CashReportRow(int Id, DateTime Date, TransactionType Type, string Activity, string Category, decimal Amount, string Currency, int? Persons, int? Dives, string Reservation, string? Description);
public record DiverReportRow(string Customer, DateTime Date, string DiveSite, string Instructor, decimal MaxDepthMeters, int BottomTimeMinutes, bool Verified, GasType Gas, string Buddy);
public record CustomerCountRow(int CustomerId, string Customer, int DivesInRange, int TotalLoggedDives);
public record ActivityCountRow(int? ActivityId, string Activity, long Transactions, long Persons, long Dives);
public record ReportSnapshot(List<object> Rows, List<ActivityCountRow> ActivityTotals);

public sealed class ReportService(IDbContextFactory<DiveCenterDbContext> factory)
{
    public async Task<List<object>> RunAsync(ReportFilter filter) => (await GetAsync(filter)).Rows;

    public async Task<ReportSnapshot> GetAsync(ReportFilter filter)
    {
        Validation.Require(Enum.IsDefined(filter.Kind), "Select a valid report.");
        Validation.Require(!filter.From.HasValue || !filter.To.HasValue || filter.From.Value.Date <= filter.To.Value.Date, "The end date must be on or after the start date.");
        Validation.Require(!filter.Year.HasValue || filter.Year is >= 1 and <= 9999, "Enter a year between 1 and 9999.");
        await using var db = await factory.CreateDbContextAsync();
        await using var snapshot = await db.Database.BeginTransactionAsync();
        var rows = filter.Kind switch
        {
            <= ReportKind.NoShows => await ReservationReportAsync(db, filter),
            ReportKind.DiveOperationsByDate => await OperationReportAsync(db, filter),
            <= ReportKind.LoggedDivesPerCustomer => await DiveReportAsync(db, filter),
            _ => await CashReportAsync(db, filter)
        };
        // Only already aggregated integer counts are combined across currency rows. Money stays separate.
        var activityTotals = filter.Kind == ReportKind.ActivityIncomeSummary
            ? rows.Cast<FinancialReportRow>().GroupBy(x => new { x.GroupId, x.Group })
                .Select(g => new ActivityCountRow(g.Key.GroupId, g.Key.Group, g.Sum(x => x.Transactions), g.Sum(x => x.Persons), g.Sum(x => x.Dives))).ToList()
            : [];
        await snapshot.CommitAsync();
        return new ReportSnapshot(rows, activityTotals);
    }

    private static IQueryable<Reservation> Bookings(DiveCenterDbContext db, ReportFilter filter)
    {
        var query = db.Reservations.AsNoTracking().InDateRange(x => x.ActivityDate, filter.From, filter.To);
        if (filter.ActivityId.HasValue) query = query.Where(x => x.ActivityId == filter.ActivityId);
        if (filter.DiveSiteId.HasValue) query = query.Where(x => x.DiveSiteId == filter.DiveSiteId);
        if (filter.InstructorId.HasValue) query = query.Where(x => x.InstructorId == filter.InstructorId);
        if (filter.CustomerId.HasValue) query = query.Where(x => x.MainCustomerId == filter.CustomerId || x.Participants.Any(p => p.CustomerId == filter.CustomerId));
        return query;
    }

    private static async Task<List<object>> ReservationReportAsync(DiveCenterDbContext db, ReportFilter filter)
    {
        var query = Bookings(db, filter);
        if (filter.Kind == ReportKind.CancelledReservations) query = query.Where(x => x.Status == ReservationStatus.Cancelled);
        if (filter.Kind == ReportKind.NoShows) query = query.Where(x => x.Status == ReservationStatus.NoShow);
        if (filter.Kind == ReportKind.ReservationsByActivity)
        {
            var grouped = await query.GroupBy(x => new { x.ActivityId, x.Activity!.Name }).Select(g => new
            {
                g.Key.ActivityId, g.Key.Name, Reservations = g.Count(),
                Participants = g.Sum(x => x.Status != ReservationStatus.Cancelled && x.Status != ReservationStatus.NoShow ? x.NumberOfParticipants : 0)
            }).OrderBy(x => x.Name).ThenBy(x => x.ActivityId).ToListAsync();
            return grouped.Select(x => new OperationalReportRow(x.Name, x.Reservations, x.Participants, 0) { GroupId = x.ActivityId }).Cast<object>().ToList();
        }
        if (filter.Kind == ReportKind.ParticipantCounts)
        {
            var grouped = await query.GroupBy(x => x.ActivityDate.Date).Select(g => new
            {
                Date = g.Key, Reservations = g.Count(),
                Participants = g.Sum(x => x.Status != ReservationStatus.Cancelled && x.Status != ReservationStatus.NoShow ? x.NumberOfParticipants : 0)
            }).OrderBy(x => x.Date).ToListAsync();
            return grouped.Select(x => new OperationalReportRow(x.Date.ToString("dd MMM yyyy"), x.Reservations, x.Participants, 0)).Cast<object>().ToList();
        }
        var rows = await query.OrderBy(x => x.ActivityDate).ThenBy(x => x.Id).Select(x => new ReservationReportRow(
            x.Id, x.ReservationNumber, x.ActivityDate, x.StartTime, x.Activity!.Name, x.NumberOfParticipants,
            x.MainCustomer == null ? "" : x.MainCustomer.FirstName + " " + x.MainCustomer.LastName,
            x.Instructor == null ? "" : x.Instructor.FirstName + " " + x.Instructor.LastName,
            x.DiveSite == null ? "" : x.DiveSite.Name, x.Boat == null ? "" : x.Boat.Name, x.Status)).ToListAsync();
        return rows.OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).Cast<object>().ToList();
    }

    private static async Task<List<object>> OperationReportAsync(DiveCenterDbContext db, ReportFilter filter)
    {
        var query = db.DiveOperations.AsNoTracking().InDateRange(x => x.Date, filter.From, filter.To);
        if (filter.ActivityId.HasValue) query = query.Where(x => x.Reservation != null && x.Reservation.ActivityId == filter.ActivityId);
        if (filter.DiveSiteId.HasValue) query = query.Where(x => x.DiveSiteId == filter.DiveSiteId);
        if (filter.InstructorId.HasValue) query = query.Where(x => x.LeadInstructorId == filter.InstructorId);
        if (filter.CustomerId.HasValue) query = query.Where(x => x.Participants.Any(p => p.CustomerId == filter.CustomerId));
        var rows = await query.OrderBy(x => x.Date).ThenBy(x => x.Id).Select(x => new OperationReportRow(
            x.Id, x.Date, x.StartTime, x.DiveSite!.Name,
            x.LeadInstructor == null ? "Unassigned" : x.LeadInstructor.FirstName + " " + x.LeadInstructor.LastName,
            x.Boat == null ? "" : x.Boat.Name, x.DiveType, x.Participants.Count,
            x.Reservation == null ? "" : x.Reservation.ReservationNumber, x.Status)).ToListAsync();
        return rows.OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).Cast<object>().ToList();
    }

    private static IQueryable<DiveLog> Logs(DiveCenterDbContext db, ReportFilter filter)
    {
        var query = db.DiveLogs.AsNoTracking().InDateRange(x => x.Date, filter.From, filter.To);
        if (filter.ActivityId.HasValue) query = query.Where(x => x.DiveOperation!.Reservation != null && x.DiveOperation.Reservation.ActivityId == filter.ActivityId);
        if (filter.DiveSiteId.HasValue) query = query.Where(x => x.DiveSiteId == filter.DiveSiteId);
        return query;
    }

    private static async Task<List<object>> DiveReportAsync(DiveCenterDbContext db, ReportFilter filter)
    {
        var logs = Logs(db, filter);
        if (filter.Kind is ReportKind.DivesByDiveSite or ReportKind.DivesByInstructor)
        {
            if (filter.InstructorId.HasValue) logs = logs.Where(x => x.DiveOperation!.LeadInstructorId == filter.InstructorId);
            if (filter.CustomerId.HasValue) logs = logs.Where(x => x.DiverLogs.Any(p => p.CustomerId == filter.CustomerId));
            var byInstructor = filter.Kind == ReportKind.DivesByInstructor;
            var groups = await logs.GroupBy(x => new
            {
                Id = byInstructor ? x.DiveOperation!.LeadInstructorId : (int?)x.DiveSiteId,
                Name = byInstructor ? x.DiveOperation!.LeadInstructor == null ? "Unassigned" : x.DiveOperation.LeadInstructor.FirstName + " " + x.DiveOperation.LeadInstructor.LastName : x.DiveSite!.Name
            }).Select(g => new
            {
                g.Key.Id, g.Key.Name, Dives = g.Count(),
                Participants = g.Sum(x => x.DiverLogs.Count(p => !filter.CustomerId.HasValue || p.CustomerId == filter.CustomerId))
            }).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync();
            return groups.Select(x => new OperationalReportRow(x.Name, 0, x.Participants, x.Dives) { GroupId = x.Id }).Cast<object>().ToList();
        }
        var personal = logs.SelectMany(x => x.DiverLogs);
        if (filter.CustomerId.HasValue) personal = personal.Where(x => x.CustomerId == filter.CustomerId);
        if (filter.InstructorId.HasValue) personal = personal.Where(x => x.InstructorId == filter.InstructorId);
        if (filter.Kind == ReportKind.CustomerDiveHistory)
            return (await personal.OrderByDescending(x => x.DiveLog!.Date).ThenBy(x => x.Id).Select(x => new DiverReportRow(
                x.Customer == null ? x.ParticipantName + " (visitor)" : x.Customer.FirstName + " " + x.Customer.LastName,
                x.DiveLog!.Date, x.DiveLog.DiveSite!.Name, x.Instructor == null ? "" : x.Instructor.FirstName + " " + x.Instructor.LastName,
                x.MaxDepthMeters ?? x.DiveLog.MaxDepthMeters, x.BottomTimeMinutes ?? x.DiveLog.BottomTimeMinutes,
                x.IsVerified, x.GasType, x.BuddyCustomer == null ? "" : x.BuddyCustomer.FirstName + " " + x.BuddyCustomer.LastName)).ToListAsync()).Cast<object>().ToList();
        return (await db.Customers.AsNoTracking().Where(x => !filter.CustomerId.HasValue || x.Id == filter.CustomerId)
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.Id)
            .Select(x => new CustomerCountRow(x.Id, x.FirstName + " " + x.LastName, personal.Count(p => p.CustomerId == x.Id),
                x.PriorLoggedDives + db.DiverDiveLogs.Count(p => p.CustomerId == x.Id))).ToListAsync()).Cast<object>().ToList();
    }

    private static IQueryable<Transaction> Cash(DiveCenterDbContext db, ReportFilter filter)
    {
        var query = db.Transactions.AsNoTracking().InDateRange(x => x.TransactionDate, filter.From, filter.To);
        if (filter.ActivityId.HasValue) query = query.Where(x => x.ActivityId == filter.ActivityId);
        if (filter.CurrencyId.HasValue) query = query.Where(x => x.CurrencyId == filter.CurrencyId);
        if (filter.CustomerId.HasValue) query = query.Where(x => x.Reservation != null && (x.Reservation.MainCustomerId == filter.CustomerId || x.Reservation.Participants.Any(p => p.CustomerId == filter.CustomerId)));
        if (filter.DiveSiteId.HasValue) query = query.Where(x => x.Reservation != null && x.Reservation.DiveSiteId == filter.DiveSiteId);
        if (filter.InstructorId.HasValue) query = query.Where(x => x.Reservation != null && x.Reservation.InstructorId == filter.InstructorId);
        return query;
    }

    private static async Task<List<object>> CashReportAsync(DiveCenterDbContext db, ReportFilter filter)
    {
        if (filter.Kind == ReportKind.MonthlySummary)
        {
            var year = filter.Year ?? filter.From?.Year ?? filter.To?.Year ?? DateTime.Today.Year;
            var start = new DateTime(year, 1, 1); var end = new DateTime(year, 12, 31);
            var query = Cash(db, filter with { From = start, To = end });
            var totals = await query.GroupBy(x => new { Month = x.TransactionDate.Month, x.CurrencyId }).Select(g => new
            {
                g.Key.Month, g.Key.CurrencyId, Transactions = g.LongCount(), Persons = g.Sum(x => (long)(x.PersonCount ?? 0)), Dives = g.Sum(x => (long)(x.DiveCount ?? 0)),
                Income = g.Sum(x => x.TransactionType == TransactionType.Income ? x.Amount : 0m),
                Expense = g.Sum(x => x.TransactionType == TransactionType.Expense ? x.Amount : 0m)
            }).ToDictionaryAsync(x => (x.Month, x.CurrencyId));
            var currencies = await db.Currencies.AsNoTracking()
                .Where(x => (!filter.CurrencyId.HasValue || x.Id == filter.CurrencyId) && (x.IsActive || x.Id == filter.CurrencyId || query.Any(t => t.CurrencyId == x.Id)))
                .OrderBy(x => x.Code).Select(x => new { x.Id, x.Code }).ToListAsync();
            var result = new List<object>();
            for (var month = 1; month <= 12; month++)
                foreach (var currency in currencies)
                {
                    totals.TryGetValue((month, currency.Id), out var total);
                    result.Add(new FinancialReportRow(CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month), currency.Code,
                        total?.Transactions ?? 0, total?.Persons ?? 0, total?.Dives ?? 0, total?.Income ?? 0, total?.Expense ?? 0) { Year = year, Month = month });
                }
            return result;
        }
        var cash = Cash(db, filter);
        var income = filter.Kind is ReportKind.IncomeByDate or ReportKind.IncomeByCategory or ReportKind.ActivityIncomeSummary;
        cash = cash.Where(x => x.TransactionType == (income ? TransactionType.Income : TransactionType.Expense));
        if (filter.Kind is ReportKind.IncomeByDate or ReportKind.ExpenseByDate)
            return (await cash.OrderBy(x => x.Currency!.Code).ThenBy(x => x.TransactionDate).ThenBy(x => x.Id).Select(x => new CashReportRow(
                x.Id, x.TransactionDate, x.TransactionType, x.Activity == null ? "No activity" : x.Activity.Name,
                x.Category == null ? "Uncategorized" : x.Category.Name, x.Amount, x.Currency!.Code, x.PersonCount, x.DiveCount,
                x.Reservation == null ? "" : x.Reservation.ReservationNumber, x.Description)).ToListAsync()).Cast<object>().ToList();
        var byActivity = filter.Kind == ReportKind.ActivityIncomeSummary;
        var groups = await cash.GroupBy(x => new
        {
            GroupId = byActivity ? x.ActivityId : x.CategoryId,
            Name = byActivity ? x.Activity == null ? "No activity" : x.Activity.Name : x.Category == null ? "Uncategorized" : x.Category.Name,
            x.CurrencyId, Currency = x.Currency!.Code
        }).Select(g => new
        {
            g.Key.GroupId, g.Key.Name, g.Key.Currency, Transactions = g.LongCount(),
            Persons = g.Sum(x => (long)(x.PersonCount ?? 0)), Dives = g.Sum(x => (long)(x.DiveCount ?? 0)),
            Income = g.Sum(x => x.TransactionType == TransactionType.Income ? x.Amount : 0m),
            Expense = g.Sum(x => x.TransactionType == TransactionType.Expense ? x.Amount : 0m)
        }).OrderBy(x => x.Name).ThenBy(x => x.GroupId).ThenBy(x => x.Currency).ToListAsync();
        return groups.Select(x => new FinancialReportRow(x.Name, x.Currency, x.Transactions, x.Persons, x.Dives, x.Income, x.Expense) { GroupId = x.GroupId }).Cast<object>().ToList();
    }
}
