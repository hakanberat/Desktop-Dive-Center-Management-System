using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using DiveCenterManager.Data;
using DiveCenterManager.Models;
using DiveCenterManager.Services;
using DiveCenterManager.ViewModels;
using DiveCenterManager.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Activity = DiveCenterManager.Models.Activity;

namespace DiveCenterManager.Diagnostics;

public sealed partial class AcceptanceRunner
{
    public async Task RunPhase7Async()
    {
        var probe = new Phase7SqlProbe();
        var factory = new Phase7QueryFactory(new DbContextOptionsBuilder<DiveCenterDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString())
            .AddInterceptors(probe).Options);
        var dashboard = new DashboardService(factory); var reports = new ReportService(factory);
        var day = DateTime.Today;
        var empty = await dashboard.GetAsync(day);
        Check(empty.TodayReservations == 0 && empty.Participants == 0 && empty.RecentReservations.Count == 0 && empty.Cash.Count == 4 && empty.Cash.All(x => x.CurrentBalance == 0), "Empty migrated SQLite produces zero dashboard values without business seed data");
        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            var rows = await reports.RunAsync(new(kind, day, day));
            Check(kind == ReportKind.MonthlySummary ? rows.Count == 48 && rows.Cast<FinancialReportRow>().All(x => x.NetCash == 0) : rows.Count == 0, "Empty SQLite report executes: " + kind);
        }
        var main = services.GetRequiredService<MainViewModel>();
        var folder = Path.Combine(paths.DataDirectory, "phase7-screenshots"); Directory.CreateDirectory(folder);
        await main.OpenAsync(main.Navigation.Single(x => x.Title == "Dashboard"));
        await RenderAsync(new DashboardView { DataContext = main.Current }, Path.Combine(folder, "Dashboard-Empty.png"), 1150, 1050);

        // Fixtures exist only in the explicit diagnostic database, through the real EF model.
        await using var db = await factory.CreateDbContextAsync();
        Check(!db.Database.HasPendingModelChanges(), "Phase 7 query changes require no schema migration");
        var money = await db.Currencies.ToDictionaryAsync(x => x.Code);
        var ada = new Customer { FirstName = "Ada", LastName = "Kaya", PriorLoggedDives = 10, TotalLoggedDives = 12 };
        var bob = new Customer { FirstName = "Deniz", LastName = "Yilmaz", TotalLoggedDives = 1 };
        var zero = new Customer { FirstName = "No", LastName = "Logs" };
        var lead = new Staff { FirstName = "Ece", LastName = "Deniz" };
        var personal = new Staff { FirstName = "Ece", LastName = "Deniz" };
        var roleId = await db.StaffRoles.Where(x => x.Name == "Instructor").Select(x => x.Id).SingleAsync();
        lead.StaffRoles.Add(new() { StaffRoleId = roleId }); personal.StaffRoles.Add(new() { StaffRoleId = roleId });
        var activity = new Activity { Name = "Guided Dive", ActivityType = ActivityType.GuidedDive };
        var otherActivity = new Activity { Name = "Guided Dive", ActivityType = ActivityType.GuidedDive };
        var site = new DiveSite { Name = "Blue Reef" }; var otherSite = new DiveSite { Name = "Blue Reef" };
        var category = new TransactionCategory { Name = "Equipment", Type = CategoryType.Both };
        var otherCategory = new TransactionCategory { Name = "Equipment", Type = CategoryType.Both };
        var cad = new Currency { Code = "CAD", Name = "Canadian Dollar", Symbol = "C$", IsActive = false };
        db.AddRange(ada, bob, zero, lead, personal, activity, otherActivity, site, otherSite, category, otherCategory, cad);
        await db.SaveChangesAsync();
        var serial = 0;
        Reservation Booking(DateTime date, ReservationStatus status, params ReservationParticipant[] people) => new()
        {
            ReservationNumber = $"RES-{day.Year}-{++serial:000000}", ActivityDate = date, ActivityId = activity.Id,
            Status = status, InstructorId = lead.Id, DiveSiteId = site.Id, Participants = [.. people], NumberOfParticipants = people.Length
        };
        ReservationParticipant Known(Customer customer) => new() { CustomerId = customer.Id, FirstName = customer.FirstName, LastName = customer.LastName };
        ReservationParticipant Visitor(string name) => new() { FirstName = name, LastName = "Visitor" };
        var booking = Booking(day, ReservationStatus.Confirmed, Known(ada), Visitor("Temporary")); booking.StartTime = TimeSpan.FromHours(11);
        var lastDay = Booking(day.AddDays(6), ReservationStatus.Pending, Known(ada));
        var outside = Booking(day.AddDays(7), ReservationStatus.Pending, Visitor("Outside"));
        var cancelled = Booking(day, ReservationStatus.Cancelled, Visitor("Cancelled"));
        var noShow = Booking(day, ReservationStatus.NoShow, Visitor("Absent"));
        var yesterday = Booking(day.AddDays(-1), ReservationStatus.Confirmed, Visitor("Earlier booking"));
        var secondBooking = Booking(day, ReservationStatus.Confirmed, Known(ada)); secondBooking.ActivityId = otherActivity.Id;
        db.AddRange(booking, lastDay, outside, cancelled, noShow, yesterday, secondBooking); await db.SaveChangesAsync();
        DiveOperation Copied(Reservation source, OperationStatus status) => new()
        {
            ReservationId = source.Id, Date = day, DiveSiteId = site.Id, LeadInstructorId = lead.Id, Status = status,
            Participants = source.Participants.Select(p => new DiveOperationParticipant { CustomerId = p.CustomerId, ReservationParticipantId = p.Id, FirstName = p.FirstName, LastName = p.LastName }).ToList()
        };
        DiveOperation Manual(DateTime date, OperationStatus status) => new()
        {
            Date = date, DiveSiteId = otherSite.Id, LeadInstructorId = personal.Id, Status = status,
            Participants = [new() { CustomerId = bob.Id, FirstName = bob.FirstName, LastName = bob.LastName }]
        };
        var completed = Copied(booking, OperationStatus.Completed);
        var planned = Copied(booking, OperationStatus.Planned);
        var manual = Manual(day, OperationStatus.Completed);
        var inProgress = Manual(day, OperationStatus.InProgress);
        inProgress.Participants.Add(new() { FirstName = "Staff", LastName = "Member", Role = ParticipantRole.Instructor });
        var cancelledOp = Manual(day, OperationStatus.Cancelled);
        cancelledOp.Participants.Add(new() { FirstName = "Excluded", LastName = "Visitor" });
        var rescheduled = Copied(yesterday, OperationStatus.Planned);
        var oldOperations = Enumerable.Range(1, 7).Select(i => Manual(day.AddDays(-i), OperationStatus.Completed)).ToArray();
        db.AddRange(completed, planned, manual, inProgress, cancelledOp, rescheduled); db.AddRange(oldOperations); await db.SaveChangesAsync();
        var log = new DiveLog
        {
            DiveOperationId = completed.Id, DiveSiteId = site.Id, Date = day, MaxDepthMeters = 18, BottomTimeMinutes = 45,
            DiverLogs = [new() { CustomerId = ada.Id, ParticipantName = ada.FullName, InstructorId = personal.Id, BuddyCustomerId = bob.Id, MaxDepthMeters = 16, BottomTimeMinutes = 40, GasType = GasType.Nitrox, OxygenPercentage = 32, IsVerified = true },
                new() { ParticipantName = "Temporary Visitor", InstructorId = lead.Id }]
        };
        var manualLog = new DiveLog { DiveOperationId = manual.Id, DiveSiteId = otherSite.Id, Date = day, MaxDepthMeters = 20, BottomTimeMinutes = 50, DiverLogs = [new() { CustomerId = bob.Id, ParticipantName = bob.FullName, InstructorId = personal.Id }] };
        var oldLog = new DiveLog { DiveOperationId = oldOperations[0].Id, DiveSiteId = otherSite.Id, Date = day.AddDays(-1), DiverLogs = [new() { CustomerId = ada.Id, ParticipantName = ada.FullName }] };
        db.AddRange(log, manualLog, oldLog);
        Transaction Cash(string currency, decimal amount, DateTime date, TransactionType type = TransactionType.Income, int? activityId = null, int? categoryId = null, int? reservationId = null, int? persons = null, int? dives = null) => new()
        {
            CurrencyId = money[currency].Id, Amount = amount, TransactionDate = date, TransactionType = type,
            ActivityId = activityId, CategoryId = categoryId, ReservationId = reservationId, PersonCount = persons, DiveCount = dives, Description = "Phase 7 isolated verification"
        };
        money.Add("CAD", cad);
        db.AddRange(Cash("EUR", .1m, day.AddHours(13), activityId: activity.Id, categoryId: category.Id, reservationId: booking.Id, persons: 2, dives: 3),
            Cash("EUR", .2m, day.AddDays(1).AddTicks(-1), activityId: activity.Id, categoryId: category.Id, reservationId: booking.Id, persons: 3, dives: 4),
            Cash("EUR", .05m, day, TransactionType.Expense, activity.Id, category.Id, booking.Id),
            Cash("TRY", 1785m, day, activityId: activity.Id, categoryId: category.Id, reservationId: booking.Id, persons: 4, dives: 5),
            Cash("EUR", 10m, day, activityId: otherActivity.Id, categoryId: otherCategory.Id),
            Cash("EUR", 7m, day.AddYears(-1)), Cash("EUR", 999m, day.AddDays(1)), Cash("CAD", 3m, day),
            Cash("USD", .1234567890123456789012345678m, day), Cash("USD", .2m, day));
        await db.SaveChangesAsync();
        var snapshot = await dashboard.GetAsync(day);
        Check(snapshot.TodayReservations == 2 && snapshot.PendingReservations == 2 && snapshot.CompletedReservations == 0, "Dashboard status and date counts exclude cancelled/no-show bookings and select pending or confirmed reservations");
        Check(snapshot.Participants == 3, "Reservation headcount uses the saved count and excludes legacy operations and staff");
        Check(snapshot.Upcoming.Select(x => x.Id).SequenceEqual(new[] { secondBooking.Id, booking.Id, lastDay.Id }), "Upcoming range includes today through day six in time order and excludes day seven");
        Check(snapshot.RecentReservations.Count == 0, "Completed legacy operations do not mark bookings completed");
        Check(snapshot.Cash.Single(x => x.Code == "EUR") is { TodayIncome: 10.3m, TodayExpense: .05m, CurrentBalance: 1016.25m }, "Dashboard separates today's exact EUR movement from all saved EUR cash");
        Check(snapshot.Cash.Single(x => x.Code == "USD").TodayIncome == .3234567890123456789012345678m && snapshot.Cash.Single(x => x.Code == "TRY").CurrentBalance == 1785m && snapshot.Cash.Single(x => x.Code == "CAD").CurrentBalance == 3m && snapshot.Cash.Single(x => x.Code == "GBP").CurrentBalance == 0, "Decimal precision, inactive currencies with history and active zero-balance currencies survive SQL aggregation");
        var results = new Dictionary<ReportKind, ReportSnapshot>();
        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            results[kind] = await reports.GetAsync(new(kind, day, day));
            Check(results[kind].Rows.Count > 0, "Populated SQLite report executes: " + kind);
        }
        Check(results[ReportKind.ReservationsByDate].Rows.Count == 4 && results[ReportKind.CancelledReservations].Rows.Cast<ReservationReportRow>().Single().Id == cancelled.Id && results[ReportKind.NoShows].Rows.Cast<ReservationReportRow>().Single().Id == noShow.Id, "Reservation date, cancelled and no-show reports return the exact saved statuses");
        var activities = results[ReportKind.ReservationsByActivity].Rows.Cast<OperationalReportRow>().ToList();
        Check(activities.Count == 2 && activities.Single(x => x.GroupId == activity.Id) is { Reservations: 3, Participants: 2 } && activities.Single(x => x.GroupId == otherActivity.Id).Participants == 1, "Equal activity names retain their identities; cancelled/no-show participants do not inflate activity totals");
        Check(results[ReportKind.ParticipantCounts].Rows.Cast<OperationalReportRow>().Single() is { Reservations: 4, Participants: 3 }, "Participant report counts actual reservation participants by activity date");
        Check(results[ReportKind.DiveOperationsByDate].Rows.Count == 6, "Operation period report includes all six statuses/assignments saved today");
        Check(results[ReportKind.DivesByDiveSite].Rows.Cast<OperationalReportRow>().Count() == 2 && results[ReportKind.DivesByInstructor].Rows.Cast<OperationalReportRow>().Count() == 2, "Equal site and instructor names remain separate groups by ID");
        var narrowed = new ReportFilter(ReportKind.ReservationsByDate, day, day, activity.Id, ada.Id, site.Id, lead.Id);
        Check((await reports.RunAsync(narrowed)).Cast<ReservationReportRow>().Single().Id == booking.Id, "Activity, participant customer, site, instructor and inclusive dates combine in reservation SQL");
        Check((await reports.RunAsync(narrowed with { Kind = ReportKind.DiveOperationsByDate })).Count == 2, "Operation filters include only matching reservation-linked dives");
        Check((await reports.RunAsync(narrowed with { Kind = ReportKind.DivesByDiveSite })).Cast<OperationalReportRow>().Single() is { Dives: 1, Participants: 1 }, "Site grouping counts shared dives once and limits participants to the selected customer");
        var personalFilter = narrowed with { Kind = ReportKind.CustomerDiveHistory, InstructorId = personal.Id };
        var history = (await reports.RunAsync(personalFilter)).Cast<DiverReportRow>().Single();
        Check(history is { MaxDepthMeters: 16m, BottomTimeMinutes: 40, Gas: GasType.Nitrox, Verified: true } && history.Buddy == bob.FullName, "Customer history filters the personal instructor and uses depth/time overrides, gas, buddy and verification");
        var visitor = results[ReportKind.CustomerDiveHistory].Rows.Cast<DiverReportRow>().Single(x => x.Customer.Contains("visitor", StringComparison.Ordinal));
        Check(visitor.MaxDepthMeters == 18 && visitor.BottomTimeMinutes == 45, "Temporary diver history falls back to shared measurements");
        var counts = results[ReportKind.LoggedDivesPerCustomer].Rows.Cast<CustomerCountRow>().ToDictionary(x => x.CustomerId);
        Check(counts[ada.Id] is { DivesInRange: 1, TotalLoggedDives: 12 } && counts[zero.Id].DivesInRange == 0, "Customer totals preserve prior history and all-time logs while period counts use real filtered logs");
        Check((await reports.RunAsync(personalFilter with { Kind = ReportKind.LoggedDivesPerCustomer })).Cast<CustomerCountRow>().Single().DivesInRange == 1, "Logged dives per customer applies every personal history filter");
        Check(results[ReportKind.IncomeByDate].Rows.Count == 7 && results[ReportKind.ExpenseByDate].Rows.Cast<CashReportRow>().Single().Amount == .05m, "Cash date reports include the entire final day and exclude adjacent days/years");
        var categoryRows = results[ReportKind.IncomeByCategory].Rows.Cast<FinancialReportRow>().ToList();
        Check(categoryRows.Single(x => x.GroupId == category.Id && x.Currency == "EUR").Income == .3m && categoryRows.Single(x => x.GroupId == otherCategory.Id && x.Currency == "EUR").Income == 10m && categoryRows.Any(x => x.GroupId == null && x.Currency == "USD"), "Category SQL sums decimals per identity and currency, including uncategorized cash");
        Check(results[ReportKind.ExpenseByCategory].Rows.Cast<FinancialReportRow>().Single() is { Currency: "EUR", Expense: .05m, Income: 0 }, "Expense category report does not include income");
        var activitySnapshot = results[ReportKind.ActivityIncomeSummary];
        Check(activitySnapshot.ActivityTotals.Single(x => x.ActivityId == activity.Id) is { Transactions: 3, Persons: 9, Dives: 12 } && activitySnapshot.Rows.Cast<FinancialReportRow>().Count(x => x.GroupId == activity.Id) == 2, "Activity totals combine only integer counts; EUR and TRY income remain separate rows");
        var currencyFiltered = await reports.GetAsync(new(ReportKind.ActivityIncomeSummary, day, day, activity.Id, ada.Id, CurrencyId: money["EUR"].Id));
        Check(currencyFiltered.Rows.Cast<FinancialReportRow>().Single().Income == .3m && currencyFiltered.ActivityTotals.Single() is { Transactions: 2, Persons: 5, Dives: 7 }, "Financial activity/customer/currency filters also apply to activity counters");
        var months = results[ReportKind.MonthlySummary].Rows.Cast<FinancialReportRow>().ToList();
        var monthIncome = 10.3m + (day.AddDays(1).Month == day.Month ? 999m : 0m);
        Check(months.Count == 60 && months.Select(x => x.Month).Distinct().Count() == 12 && months.Single(x => x.Month == day.Month && x.Currency == "EUR").Income == monthIncome, "Monthly summary uses the whole selected year, fills all twelve months and retains historical CAD");
        var previousYear = (await reports.RunAsync(new(ReportKind.MonthlySummary, null, null, Year: day.Year - 1, CurrencyId: money["EUR"].Id))).Cast<FinancialReportRow>().ToList();
        Check(previousYear.Count == 12 && previousYear.Sum(x => x.Income) == 7m && previousYear.Count(x => x.Income == 0) == 11, "Selected year and currency exclude current-year cash and show zero months");
        Check((await reports.RunAsync(new(ReportKind.MonthlySummary, null, null, Year: 9999))).Count == 48, "Maximum supported year does not overflow inclusive date bounds");
        await RejectAsync(() => reports.RunAsync(new(ReportKind.IncomeByDate, day.AddDays(1), day)), "Reversed report dates are rejected");
        await RejectAsync(() => reports.RunAsync(new(ReportKind.MonthlySummary, null, null, Year: 10000)), "Out-of-range report years are rejected");
        var sql = string.Join(Environment.NewLine, probe.Commands);
        Check(sql.Contains("ef_sum(", StringComparison.OrdinalIgnoreCase) && sql.Contains("GROUP BY", StringComparison.Ordinal) && sql.Contains("LIMIT", StringComparison.Ordinal), "Actual SQLite commands perform decimal SUM, grouping, reservation headcount and recent-row limiting on the server");
        Check(probe.Commands.Where(x => x.Contains("ef_sum(", StringComparison.OrdinalIgnoreCase)).All(x => !x.Contains("\"Description\"", StringComparison.Ordinal)), "Summary queries do not load full transaction records");
        await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "phase7-sql.txt"), probe.Commands);
        await VerifyPhase7ViewsAsync(main, day, folder);
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase7-expected.json"), JsonSerializer.Serialize(new Phase7Snapshot(day, ada.Id, activity.Id)));
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase7-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task VerifyPhase7ViewsAsync(MainViewModel main, DateTime day, string folder)
    {
        var listener = new BindingListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            await main.OpenAsync(main.Navigation.Single(x => x.Title == "Dashboard"));
            Check(main.Current is DashboardViewModel { Cards.Count: 4, Message.Length: 0 }, "Sidebar opens the real dashboard with four operational cards");
            await RenderAsync(new DashboardView { DataContext = main.Current }, Path.Combine(folder, "Dashboard.png"), 1150, 1150);
            await main.OpenAsync(main.Navigation.Single(x => x.Title == "Reports")); var vm = (ReportsViewModel)main.Current!;
            foreach (var kind in vm.ReportField.Choices.Select(x => (ReportKind)x.Value!))
            {
                vm.ReportField.SetValue(kind);
                if (kind != ReportKind.MonthlySummary) { SetOperationFieldForScreen(vm, "From", day); SetOperationFieldForScreen(vm, "To", day); }
                await vm.RefreshCommand.ExecuteAsync();
                Check(vm.Message.Length == 0 && vm.Rows.Count > 0 && vm.Columns.Count > 0, "Report picker loads usable WPF data and columns: " + kind);
                var shell = new MainWindow(main); var content = (FrameworkElement)shell.Content; shell.Content = null; content.DataContext = main;
                await RenderAsync(content, Path.Combine(folder, kind + ".png"), 1100, 720);
                var view = VisualDescendants(content).OfType<ReportsView>().Single();
                var grid = VisualDescendants(view).OfType<RecordGrid>().Single(x => AutomationProperties.GetName(x) == "Report results");
                Check(grid.ActualHeight >= 120, "Report results remain usable at minimum window size: " + kind);
            }
            Check(vm.HasActivityTotals && vm.ActivityTotals.Count > 0, "Activity report displays overall counts separately from income by currency");
            vm.ReportField.SetValue(ReportKind.MonthlySummary);
            Check(vm.VisibleFilters.Any(x => x.Key == "Year") && vm.VisibleFilters.All(x => x.Key is not "From" and not "To" and not "InstructorId"), "Monthly UI shows a year selector and applicable financial filters");
            SetOperationFieldForScreen(vm, "Year", "invalid"); await vm.RefreshCommand.ExecuteAsync();
            Check(vm.Message.Length > 0, "Invalid monthly year produces a visible validation message");
            await vm.ResetFiltersCommand.ExecuteAsync();
            Check(vm.SelectedKind == ReportKind.MonthlySummary && vm.Message.Length == 0 && vm.Rows.Count == 60, "Clear filters preserves the report and restores a valid current year");
            vm.ReportField.SetValue(ReportKind.ReservationsByDate);
            SetOperationFieldForScreen(vm, "From", day.AddDays(1)); SetOperationFieldForScreen(vm, "To", day);
            await vm.RefreshCommand.ExecuteAsync(); Check(vm.Message.Length > 0, "Reversed operational dates show a visible validation message");
            await vm.ResetFiltersCommand.ExecuteAsync();
            Check(vm.Rows.Count == 7 && vm.VisibleFilters.All(x => x.Key is not "Year" and not "CurrencyId"), "Clear date filters retrieves all reservations and hides financial-only controls");
            Check(listener.Errors.Count == 0, "Dashboard and all sixteen native report views render without binding errors");
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "phase7-binding-diagnostics.txt"), listener.Errors);
        }
    }

    public async Task VerifyPhase7PersistenceAsync()
    {
        var expected = JsonSerializer.Deserialize<Phase7Snapshot>(await File.ReadAllTextAsync(Path.Combine(paths.DataDirectory, "phase7-expected.json")))!;
        var dashboard = await services.GetRequiredService<DashboardService>().GetAsync(expected.Day);
        Check(dashboard.Participants == 3 && dashboard.Cash.Single(x => x.Code == "EUR").CurrentBalance == 1016.25m, "Restart retains dashboard headcount and exact independent cash balances");
        var reports = services.GetRequiredService<ReportService>();
        var history = (await reports.RunAsync(new(ReportKind.LoggedDivesPerCustomer, expected.Day, expected.Day, CustomerId: expected.CustomerId))).Cast<CustomerCountRow>().Single();
        Check(history is { DivesInRange: 1, TotalLoggedDives: 12 }, "Restart retains filtered customer history and prior dive totals");
        var activity = await reports.GetAsync(new(ReportKind.ActivityIncomeSummary, expected.Day, expected.Day, expected.ActivityId));
        Check(activity.ActivityTotals.Single() is { Transactions: 3, Persons: 9, Dives: 12 } && activity.Rows.Count == 2, "Restart retains activity counters and separate currency groups");
        Check((await reports.RunAsync(new(ReportKind.MonthlySummary, null, null, Year: expected.Day.Year))).Count == 60, "Restart recreates all twelve monthly currency summaries from saved cash");
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase7-persistence-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record Phase7Snapshot(DateTime Day, int CustomerId, int ActivityId);
    private sealed class Phase7QueryFactory(DbContextOptions<DiveCenterDbContext> options) : IDbContextFactory<DiveCenterDbContext>
    {
        public DiveCenterDbContext CreateDbContext() => new(options);
        public Task<DiveCenterDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
    private sealed class Phase7SqlProbe : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
