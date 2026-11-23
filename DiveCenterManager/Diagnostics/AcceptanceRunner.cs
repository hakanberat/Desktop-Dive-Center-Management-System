using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiveCenterManager.Data;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;
using DiveCenterManager.ViewModels;
using DiveCenterManager.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Activity = DiveCenterManager.Models.Activity;

namespace DiveCenterManager.Diagnostics;

// Runs only with --self-test and an explicit isolated --data-dir. Uses production services,
// real migrations, on-disk SQLite, WPF templates and bindings. No substitute persistence.
public sealed partial class AcceptanceRunner(ServiceProvider services, AppPaths paths)
{
    private readonly List<string> _passed = [];
    private void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        _passed.Add(name);
        File.AppendAllText(Path.Combine(paths.DataDirectory, "acceptance-progress.txt"), "PASS " + name + Environment.NewLine);
    }
    private async Task RejectAsync(Func<Task> action, string name)
    {
        try { await action(); }
        catch (ValidationException) { Check(true, name); return; }
        throw new InvalidOperationException("Expected validation rejection: " + name);
    }
    public async Task RunAsync()
    {
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        var masters = services.GetRequiredService<MasterDataService>();
        var reservations = services.GetRequiredService<ReservationService>();
        var operations = services.GetRequiredService<DiveOperationService>();
        var logbook = services.GetRequiredService<LogbookService>();
        var customers = services.GetRequiredService<CustomerService>();
        var cash = services.GetRequiredService<TransactionService>();
        var dashboard = services.GetRequiredService<DashboardService>();
        var reports = services.GetRequiredService<ReportService>();
        var lookups = services.GetRequiredService<LookupService>();
        var today = DateTime.Today;
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "tr-TR", "ro-RO" })
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
                Services.Validation.Check(new DiverDiveLog { ParticipantName = "Locale check", TankSizeLiters = 12.5m, GasType = GasType.Nitrox, OxygenPercentage = 32m });
                Services.Validation.Check(new Transaction { CurrencyId = 1, Amount = .0001m });
                var field = new FieldViewModel("Amount", "Amount", typeof(decimal), 12.5m);
                Check((decimal)field.Read()! == 12.5m, "Decimal validation and editor round-trip work in " + culture);
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
        await using (var db = await factory.CreateDbContextAsync())
        {
            Check(!await db.Customers.AnyAsync() && !await db.Reservations.AnyAsync() && !await db.Transactions.AnyAsync(), "Production initialization contains no sample business data");
            Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 2, "Both Code First migrations apply to a fresh SQLite file");
            Check(!db.Database.HasPendingModelChanges(), "EF model matches its migration snapshot");
            Check(await db.Currencies.CountAsync() == 4, "TRY, EUR, USD and GBP are initialized independently");
        }
        var currencyList = (await masters.ListAsync(typeof(Currency))).Cast<Currency>().ToDictionary(x => x.Code);
        var diver = new Customer { FirstName = "Ada", LastName = "Kaya", Email = "ada@example.test", TotalLoggedDives = 12, LastDiveDate = today.AddYears(-1) };
        var other = new Customer { FirstName = "Emre", LastName = "Deniz" };
        await masters.SaveAsync(diver); await masters.SaveAsync(other);
        Check(diver.Id > 0 && diver.CertificationAgency == null, "Create a customer without certification");
        var roles = (await masters.ListAsync(typeof(StaffRole))).Cast<StaffRole>().ToList();
        var instructor = new Staff { FirstName = "Test", LastName = "Instructor", StaffRoles = [new() { StaffRoleId = roles.Single(x => x.Name == "Instructor").Id }, new() { StaffRoleId = roles.Single(x => x.Name == "Captain").Id }] };
        await masters.SaveAsync(instructor);
        Check(((Staff)(await masters.ListAsync(typeof(Staff))).Single()).StaffRoles.Count == 2, "Staff saves multiple roles in the join table");
        var captain = new Staff { FirstName = "Test", LastName = "Captain", StaffRoles = [new() { StaffRoleId = roles.Single(x => x.Name == "Captain").Id }] }; await masters.SaveAsync(captain);
        var instructors = await lookups.GetAsync("InstructorId");
        Check(instructors.Count == 1 && Equals(instructors[0].Value, instructor.Id), "Instructor lookup excludes staff with no Instructor role");
        var activity = new Activity { Name = "Acceptance Try Dive", ActivityType = ActivityType.TryDive, DefaultPrice = 75m, DefaultCurrencyId = currencyList["EUR"].Id }; await masters.SaveAsync(activity);
        var site = new DiveSite { Name = "Acceptance shore", Location = "Test site" }; await masters.SaveAsync(site);
        var boat = new Boat { Name = "Acceptance boat", Capacity = 6 }; await masters.SaveAsync(boat);
        var equipment = new Equipment { EquipmentType = EquipmentType.Regulator, SerialNumber = "ACCEPT-001" }; await masters.SaveAsync(equipment);
        equipment.Status = EquipmentStatus.Maintenance; await masters.SaveAsync(equipment);
        Check(((Equipment)(await masters.ListAsync(typeof(Equipment))).Single()).Status == EquipmentStatus.Maintenance, "Equipment inventory can be created and edited");
        var reservation = NewReservation();
        await reservations.SaveAsync(reservation);
        Check(reservation.ReservationNumber == $"RES-{today.Year}-000001", "Reservation number is generated atomically in the expected format");
        reservation = await reservations.GetAsync(reservation.Id);
        Check(reservation.Participants.Count == 3 && reservation.NumberOfParticipants == 3 && reservation.Participants.Count(x => x.CustomerId == null) == 1, "One reservation persists multiple participants including a temporary visitor");
        Check((await reservations.ListAsync(new ReservationFilter(today, today, activity.Id, ReservationStatus.Confirmed, "Ada"))).Count == 1, "Reservation date, activity, status and participant search filters combine");
        var main = services.GetRequiredService<MainViewModel>();
        var daily = main.Navigation.Single(x => x.Title == "Calendar").Create(); await daily.RefreshAsync();
        Check(daily.Rows.Cast<Reservation>().Any(x => x.Id == reservation.Id), "Reservation appears in Calendar");
        var calendar = (ScheduleViewModel)main.Navigation.Single(x => x.Title == "Calendar").Create(); await calendar.RefreshAsync();
        Check(calendar.Days.Count == 7 && calendar.Rows.Count == 1, "Week calendar groups a chronological seven-day range");
        calendar.Filters.Single(x => x.Key == "View").SelectedValue = "Day"; await calendar.RefreshAsync();
        Check(calendar.Days.Count == 1 && calendar.Rows.Count == 1, "Day calendar filters the selected date");
        var operation = await operations.DraftFromReservationAsync(reservation.Id);
        Check(operation.Id == 0 && operation.Participants.Count == 3 && operation.LeadInstructorId == instructor.Id && operation.DiveSiteId == site.Id && operation.BoatId == boat.Id, "Reservation conversion produces an unsaved, editable operation draft with assignments");
        operation.Participants[0].BuddyGroup = "A";
        await operations.SaveAsync(operation);
        await RejectAsync(() => logbook.DraftFromOperationAsync(operation.Id), "A planned operation cannot create a dive log");
        await RejectAsync(() => operations.ChangeStatusAsync(operation.Id, OperationStatus.Completed), "Completing a planned operation requires starting it first");
        await operations.ChangeStatusAsync(operation.Id, OperationStatus.InProgress); await operations.ChangeStatusAsync(operation.Id, OperationStatus.Completed);
        Check((await operations.GetAsync(operation.Id)).Status == OperationStatus.Completed, "Operation starts and completes through persisted transitions");
        var log = await logbook.DraftFromOperationAsync(operation.Id); log.MaxDepthMeters = 18; log.WaterTemperatureCelsius = 25; log.VisibilityMeters = 20;
        var personal = log.DiverLogs.Single(x => x.CustomerId == diver.Id);
        personal.GasType = GasType.Nitrox; personal.OxygenPercentage = 32; personal.StartPressureBar = 200; personal.EndPressureBar = 60; personal.BuddyCustomerId = other.Id; personal.IsVerified = true;
        await logbook.SaveAsync(log);
        Check(log.Id > 0 && (await logbook.GetAsync(log.Id)).DiverLogs.Count == 3, "One shared log stores three separate personal diver records");
        var history = await customers.HistoryAsync(diver.Id);
        Check(history.Dives.Count == 1 && history.Customer.TotalLoggedDives == 13 && history.Dives[0].MaxDepthMeters == 18 && history.Dives[0].Verified, "Customer history uses shared measurements and preserves prior logged dives");
        Check(history.Reservations.Count == 1, "Customer reservation history includes participant bookings");
        var existingLog = await logbook.DraftFromOperationAsync(operation.Id); await logbook.SaveAsync(existingLog);
        Check(existingLog.Id == log.Id && (await customers.HistoryAsync(diver.Id)).Customer.TotalLoggedDives == 13, "Reopening and saving a log does not duplicate it or increment counts twice");
        existingLog = await logbook.GetAsync(log.Id); existingLog.Date = today.AddDays(-1); await logbook.SaveAsync(existingLog);
        Check((await customers.HistoryAsync(diver.Id)).Customer.LastDiveDate == today.AddDays(-1), "Correcting a dive date recalculates the customer's latest dive date");
        existingLog = await logbook.GetAsync(log.Id); existingLog.Date = today; await logbook.SaveAsync(existingLog);
        var incomeCategory = (await masters.ListAsync(typeof(TransactionCategory))).Cast<TransactionCategory>().Single(x => x.Name == "Diving");
        var fuelCategory = (await masters.ListAsync(typeof(TransactionCategory))).Cast<TransactionCategory>().Single(x => x.Name == "Fuel");
        var income = new Transaction { TransactionType = TransactionType.Income, Amount = 250.10m, CurrencyId = currencyList["EUR"].Id, ReservationId = reservation.Id, ActivityId = activity.Id, CategoryId = incomeCategory.Id, PersonCount = 3, DiveCount = 3, Description = "Acceptance reservation payment" };
        await cash.SaveAsync(income);
        var expense = new Transaction { TransactionType = TransactionType.Expense, Amount = 70.05m, CurrencyId = currencyList["EUR"].Id, CategoryId = fuelCategory.Id, Description = "Acceptance fuel expense" }; await cash.SaveAsync(expense);
        await cash.SaveAsync(new Transaction { TransactionType = TransactionType.Income, Amount = 1785.20m, CurrencyId = currencyList["TRY"].Id, Description = "Independent TRY cash" });
        await cash.SaveAsync(new Transaction { TransactionType = TransactionType.Income, Amount = 0.10m, CurrencyId = currencyList["USD"].Id });
        await cash.SaveAsync(new Transaction { TransactionType = TransactionType.Income, Amount = 0.20m, CurrencyId = currencyList["USD"].Id });
        await cash.SaveAsync(new Transaction { TransactionDate = today.AddYears(-1), TransactionType = TransactionType.Income, Amount = 999, CurrencyId = currencyList["EUR"].Id, Description = "Previous year income" });
        var balances = (await cash.BalancesAsync(new TransactionFilter(From: today, To: today))).ToDictionary(x => x.Code);
        Check(balances.Count == 4 && balances["EUR"].Balance == 180.05m && balances["TRY"].Balance == 1785.20m && balances["USD"].Balance == 0.30m && balances["GBP"].Balance == 0, "Date-filtered TRY/EUR/USD/GBP balances remain independent and decimal-exact");
        var filtered = await cash.ListAsync(new TransactionFilter(today.Year, today.Month, TransactionType.Income, currencyList["EUR"].Id, incomeCategory.Id, activity.Id, "acceptance"));
        Check(filtered.Count == 1 && filtered[0].Id == income.Id, "All seven cash-screen filters work together");
        Check((await cash.ListAsync(new TransactionFilter(Search: "fuel"))).Count == 1 && (await cash.ListAsync(new TransactionFilter(Search: "Try Dive"))).Count == 1, "Cash search matches category and activity as well as description");
        Check((await reservations.PaymentSummaryAsync(reservation.Id)).Contains("EUR") && !(await reservations.PaymentSummaryAsync(reservation.Id)).Contains("TRY"), "Reservation detail groups only its linked payments by currency");
        var snapshot = await dashboard.GetAsync(today);
        Check(snapshot.TodayReservations == 1 && snapshot.Participants == 3 && snapshot.CompletedReservations == 0 && snapshot.RecentReservations.Count == 0 && snapshot.Upcoming.Count == 1, "Dashboard operational values are queried from SQLite");
        Check(snapshot.Cash.Single(x => x.Code == "EUR").TodayIncome == 250.10m && snapshot.Cash.Single(x => x.Code == "EUR").CurrentBalance == 1179.05m, "Dashboard distinguishes today's cash from all-time balance");
        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            var report = await reports.RunAsync(new ReportFilter(kind, today, today));
            Check(report != null, "Report executes against SQLite: " + kind);
        }
        var monthly = (await reports.RunAsync(new ReportFilter(ReportKind.MonthlySummary, today, today))).Cast<FinancialReportRow>().ToList();
        Check(monthly.Count == 48 && monthly.Single(x => x.Currency == "EUR" && x.Month == today.Month).NetCash == 180.05m && monthly.Single(x => x.Currency == "USD" && x.Month == today.Month).Income == .30m, "Monthly cash report includes all twelve months and four currencies with exact sums");
        var activityReport = (await reports.RunAsync(new ReportFilter(ReportKind.ActivityIncomeSummary, today, today))).Cast<FinancialReportRow>().Single(x => x.Group == activity.Name);
        Check(activityReport.Transactions == 1 && activityReport.Persons == 3 && activityReport.Dives == 3 && activityReport.Currency == "EUR", "Activity report shows transaction, person and dive counts per currency");
        await RejectAsync(() => masters.SaveAsync(new Customer { LastName = "Only" }), "A missing first name is rejected");
        await RejectAsync(() => masters.SaveAsync(new Customer { FirstName = "A", LastName = "B", Email = "invalid" }), "Invalid email is rejected");
        await RejectAsync(() => cash.SaveAsync(new Transaction { CurrencyId = currencyList["EUR"].Id, Amount = 0 }), "Zero cash amount is rejected");
        await RejectAsync(() => cash.SaveAsync(new Transaction { CurrencyId = currencyList["EUR"].Id, Amount = -2 }), "Negative cash amount is rejected");
        await RejectAsync(() => cash.SaveAsync(new Transaction { Amount = 2 }), "Missing transaction currency is rejected");
        await RejectAsync(() => cash.SaveAsync(new Transaction { CurrencyId = currencyList["EUR"].Id, Amount = 2, PersonCount = -1 }), "Negative person counts are rejected");
        await RejectAsync(() => cash.SaveAsync(new Transaction { CurrencyId = currencyList["EUR"].Id, Amount = 2, DiveCount = -1 }), "Negative dive counts are rejected");
        await RejectAsync(() => cash.SaveAsync(new Transaction { CurrencyId = currencyList["EUR"].Id, Amount = 2, CategoryId = fuelCategory.Id, TransactionType = TransactionType.Income }), "Income cannot use an expense-only category");
        await RejectAsync(() => cash.DeleteAsync(expense.Id, false), "Transaction deletion requires explicit confirmation in the service");
        var deletable = new Transaction { CurrencyId = currencyList["GBP"].Id, Amount = 5 }; await cash.SaveAsync(deletable); await cash.DeleteAsync(deletable.Id, true);
        Check(!(await cash.ListAsync(new TransactionFilter())).Any(x => x.Id == deletable.Id), "Confirmed transaction deletion is persisted");
        foreach (var mutate in new Action<DiveLog>[] { x => x.MaxDepthMeters = -1, x => x.BottomTimeMinutes = -1, x => x.DiverLogs[0].StartPressureBar = -1, x => { x.DiverLogs[0].GasType = GasType.Nitrox; x.DiverLogs[0].OxygenPercentage = 101; }, x => { x.DiverLogs[0].GasType = GasType.Nitrox; x.DiverLogs[0].OxygenPercentage = 20; } })
        {
            var invalid = await logbook.GetAsync(log.Id); mutate(invalid); await RejectAsync(() => logbook.SaveAsync(invalid), "Invalid depth, time, pressure or oxygen measurement is rejected");
        }
        var invalidBooking = NewReservation(); invalidBooking.NumberOfParticipants = 0; await RejectAsync(() => reservations.SaveAsync(invalidBooking), "A reservation requires at least one participant");
        invalidBooking = NewReservation(); invalidBooking.InstructorId = captain.Id; await RejectAsync(() => reservations.SaveAsync(invalidBooking), "The service rejects a staff assignment without Instructor role");
        var smallBoat = new Boat { Name = "Small test boat", Capacity = 1 }; await masters.SaveAsync(smallBoat);
        invalidBooking = NewReservation(); invalidBooking.BoatId = smallBoat.Id; await RejectAsync(() => reservations.SaveAsync(invalidBooking), "Boat capacity is validated against participant count");
        var duplicate = NewReservation(); duplicate.Participants[2].CustomerId = diver.Id; await RejectAsync(() => reservations.SaveAsync(duplicate), "Duplicate linked customers in a booking are rejected");
        var linkedBooking = await reservations.GetAsync(reservation.Id); linkedBooking.Participants.RemoveAt(0); await RejectAsync(() => reservations.SaveAsync(linkedBooking), "Reservation participants used by an operation cannot be removed");
        var cancelled = NewReservation(); cancelled.Status = ReservationStatus.Cancelled; await reservations.SaveAsync(cancelled);
        await RejectAsync(() => operations.DraftFromReservationAsync(cancelled.Id), "Cancelled bookings cannot become operations");
        var noShow = NewReservation(); noShow.Status = ReservationStatus.NoShow; await reservations.SaveAsync(noShow);
        await RejectAsync(() => operations.DraftFromReservationAsync(noShow.Id), "No-show bookings cannot become operations");
        var rental = new Activity { Name = "Rental only", ActivityType = ActivityType.EquipmentRental }; await masters.SaveAsync(rental);
        var nonDiving = NewReservation(); nonDiving.ActivityId = rental.Id; nonDiving.ActivityDate = today.AddDays(10); await reservations.SaveAsync(nonDiving);
        await RejectAsync(() => operations.DraftFromReservationAsync(nonDiving.Id), "Non-diving reservations cannot become operations");
        var manual = new DiveOperation { DiveSiteId = site.Id, Participants = [new DiveOperationParticipant { FirstName = "Walk-in", LastName = "Visitor" }] }; await operations.SaveAsync(manual);
        Check(manual.Id > 0 && (await operations.GetAsync(manual.Id)).ReservationId == null, "Manual dive operations require no reservation or full customer");
        await operations.ChangeStatusAsync(manual.Id, OperationStatus.Cancelled);
        await masters.ToggleActiveAsync(diver); await masters.ToggleActiveAsync(activity); await masters.ToggleActiveAsync(site); await masters.ToggleActiveAsync(instructor); await masters.ToggleActiveAsync(boat);
        var archivedHistory = await customers.HistoryAsync(diver.Id);
        Check(!archivedHistory.Customer.IsActive && archivedHistory.Dives.Count == 1 && archivedHistory.Reservations.Count > 0, "Deactivating referenced masters preserves reservation and dive history");
        var historicalLog = await logbook.GetAsync(log.Id); historicalLog.Notes = "Corrected after master deactivation"; await logbook.SaveAsync(historicalLog);
        Check((await logbook.GetAsync(log.Id)).Notes == historicalLog.Notes, "Historical records can retain their existing inactive references when edited");
        await masters.ToggleActiveAsync(diver); await masters.ToggleActiveAsync(activity); await masters.ToggleActiveAsync(site); await masters.ToggleActiveAsync(instructor); await masters.ToggleActiveAsync(boat);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Check(db.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "All historical foreign keys use restrictive delete behavior");
            try { await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Customers WHERE Id = {diver.Id}"); throw new InvalidOperationException("Delete should have failed"); }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19) { Check(true, "SQLite itself blocks deletion of a referenced customer"); }
        }
        var backupPath = Path.Combine(paths.BackupDirectory, "acceptance-backup.db");
        await services.GetRequiredService<BackupService>().CreateAsync(backupPath);
        await using (var backup = new DiveCenterDbContext(new DbContextOptionsBuilder<DiveCenterDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = backupPath, Pooling = false }.ToString()).Options))
        {
            Check(await backup.DiveLogs.CountAsync() == 1 && await backup.Transactions.CountAsync() == 6, "Online backup includes committed WAL records and opens independently");
        }
        await RejectAsync(() => services.GetRequiredService<BackupService>().CreateAsync(paths.DatabasePath), "Backup cannot overwrite the live database");
        await ValidateEditorsAsync(masters, lookups);
        await ValidateSampleAndMigrationAsync();
        await RenderViewsAsync(main);
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "acceptance-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed, database = paths.DatabasePath, utc = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));

        Reservation NewReservation() => new()
        {
            ActivityId = activity.Id, ActivityDate = today, ReservationDate = today, Status = ReservationStatus.Confirmed, MainCustomerId = diver.Id,
            InstructorId = instructor.Id, DiveSiteId = site.Id, BoatId = boat.Id, NumberOfParticipants = 3,
            Participants = [new ReservationParticipant { CustomerId = diver.Id, FirstName = diver.FirstName, LastName = diver.LastName }, new ReservationParticipant { CustomerId = other.Id, FirstName = other.FirstName, LastName = other.LastName }, new ReservationParticipant { FirstName = "Temporary", LastName = "Visitor" }]
        };
    }

    public async Task VerifyPersistenceAsync()
    {
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Check(await db.Customers.AnyAsync(x => x.FirstName == "Ada" && x.TotalLoggedDives == 13), "A new process reopens the customer and derived dive count");
        Check(await db.DiveLogs.CountAsync() == 1 && await db.DiverDiveLogs.CountAsync() == 3, "A new process reopens normalized shared and personal logs");
        Check(await db.Transactions.CountAsync() == 6, "A new process reopens all cash transactions");
        var balances = await services.GetRequiredService<TransactionService>().BalancesAsync(new TransactionFilter());
        Check(balances.Single(x => x.Code == "EUR").Balance == 1179.05m && balances.Single(x => x.Code == "USD").Balance == .30m, "Currency balances remain exact after application restart");
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Restart has no pending migrations");
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "persistence-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task ValidateEditorsAsync(MasterDataService masters, LookupService lookups)
    {
        var customer = new Customer { FirstName = "UI", LastName = "Validation" };
        var editor = new EditorViewModel("Customer validation", customer, masters.SaveAsync); await editor.InitializeAsync(lookups, masters);
        editor.ApplyFields(); Services.Validation.Check(customer);
        Check(customer.Phone == "" && customer.Email == null && customer.Nationality == "", "Blank optional editor fields preserve nullable and non-nullable database semantics");
        var participant = new ReservationParticipant();
        editor = new EditorViewModel("Participant", participant, _ => Task.CompletedTask); await editor.InitializeAsync(lookups, masters);
        editor.Fields.Single(x => x.Key == "CustomerId").SelectedValue = ((Customer)(await masters.ListAsync(typeof(Customer))).First()).Id;
        editor.ApplyFields();
        Check(participant.FirstName.Length > 0 && participant.LastName.Length > 0, "Selecting a customer fills the participant name fields");
        editor.Fields.Single(x => x.Key == "FirstName").Text = "";
        try { editor.ApplyFields(); throw new InvalidOperationException("Required editor field was accepted"); }
        catch (ValidationException) { Check(editor.Fields.Single(x => x.Key == "FirstName").Error.Length > 0, "Required-field errors are visible on their editor field"); }
        var optionalChoice = new FieldViewModel("CurrencyId", "Currency", typeof(int?), null, lookup: true) { Choices = [new Choice(null, "All currencies")] };
        Check(optionalChoice.SelectedChoice?.Label == "All currencies", "Empty filter selections display their All / None label");
        var dateField = new FieldViewModel("Date", "Activity date", typeof(DateTime), DateTime.Today) { InputError = "Enter a valid date." };
        try { dateField.Read(); throw new InvalidOperationException("Invalid typed date was accepted"); }
        catch (ValidationException) { Check(dateField.Error.Length > 0, "Invalid typed dates block saving and show a field error"); }
        Check((string)new Converters.TimeOfDayConverter().Convert(TimeSpan.FromHours(9.5), typeof(string), "", System.Globalization.CultureInfo.CurrentCulture) == "09:30", "Schedule cards format operational times as HH:mm");
    }

    private async Task RenderViewsAsync(MainViewModel main)
    {
        var folder = Path.Combine(paths.DataDirectory, "screenshots"); Directory.CreateDirectory(folder);
        var listener = new BindingListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            foreach (var item in main.Navigation)
            {
                await main.OpenAsync(item);
                Check(main.Current != null && string.IsNullOrEmpty(main.Current.Message), "Navigation loads real screen: " + item.Title);
                var shell = new MainWindow(main);
                var content = (FrameworkElement)shell.Content; content.DataContext = main; shell.Content = null;
                await RenderAsync(content, Path.Combine(folder, item.Title.Replace(" / ", "-").Replace(" ", "-") + ".png"), 1440, 900);
            }
            var masters = services.GetRequiredService<MasterDataService>(); var lookups = services.GetRequiredService<LookupService>();
            foreach (var type in MasterDataService.SupportedTypes.Concat([typeof(Reservation), typeof(ReservationParticipant), typeof(DiveOperation), typeof(DiveOperationParticipant), typeof(DiveLog), typeof(DiverDiveLog), typeof(Transaction)]))
            {
                var entity = Activator.CreateInstance(type)!;
                var vm = new EditorViewModel("Edit " + EntitySchema.Words(type.Name), entity, _ => Task.CompletedTask); await vm.InitializeAsync(lookups, masters);
                var window = new EditorWindow { DataContext = vm };
                var content = (FrameworkElement)window.Content; content.DataContext = vm; window.Content = null;
                await RenderAsync(content, Path.Combine(folder, "Editor-" + type.Name + ".png"), 880, 780);
                Check(vm.Fields.Count > 0, "WPF editor renders: " + type.Name);
            }
            Check(listener.Errors.Count == 0, "All navigation screens and editors render without WPF binding errors");
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "binding-diagnostics.txt"), listener.Errors);
        }
    }
    private async Task ValidateSampleAndMigrationAsync()
    {
        var samplePaths = new AppPaths(Path.Combine(paths.DataDirectory, "sample-fixture"));
        await using var sampleServices = new ServiceCollection().AddDiveHub(samplePaths).BuildServiceProvider();
        await sampleServices.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        await sampleServices.GetRequiredService<DevelopmentSampleData>().AddAsync();
        await sampleServices.GetRequiredService<DevelopmentSampleData>().AddAsync();
        await using (var db = await sampleServices.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>().CreateDbContextAsync())
        {
            Check(await db.Customers.CountAsync() == 10 && await db.Reservations.CountAsync() == 8, "Opt-in sample data is isolated and idempotent");
            Check(await db.Activities.CountAsync() == 13 && await db.DiveSites.CountAsync() == 12, "Optional activity and public site defaults are editable database records");
            Check(await db.DiveLogs.CountAsync() == 2 && await db.Transactions.CountAsync() == 9, "Sample operations, logs and transactions use the real business services");
        }
        var migrationPaths = new AppPaths(Path.Combine(paths.DataDirectory, "migration-fixture"));
        await using var migrationServices = new ServiceCollection().AddDiveHub(migrationPaths).BuildServiceProvider();
        var factory = migrationServices.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
            await migrator.MigrateAsync("20260913055325_InitialCreate");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO Customers (FirstName, LastName, Phone, Nationality, TotalLoggedDives, IsActive, CreatedAt, LastDiveDate) VALUES ('Imported', 'Diver', '', '', 17, 1, '2025-01-01 00:00:00', '2025-01-01 00:00:00')");
        }
        await migrationServices.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var imported = await db.Customers.SingleAsync();
            Check(imported.PriorLoggedDives == 17 && imported.TotalLoggedDives == 17, "Upgrading an existing database preserves imported dive totals");
            Check(Directory.GetFiles(migrationPaths.BackupDirectory, "before-migration-*.db").Length == 1, "Startup creates an automatic snapshot before migrating an existing database");
        }
    }
    private static async Task RenderAsync(FrameworkElement element, string file, int width = 1150, int height = 850)
    {
        element.Width = double.NaN; element.Height = double.NaN;
        var root = new Border { Child = element, Background = (Brush)Application.Current.FindResource("BackgroundBrush") };
        System.Windows.Documents.TextElement.SetForeground(root, (Brush)Application.Current.FindResource("TextBrush"));
        System.Windows.Documents.TextElement.SetFontSize(root, 14);
        System.Windows.Documents.TextElement.SetFontFamily(root, new FontFamily("Segoe UI"));
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream = File.Create(file); encoder.Save(stream);
    }
    private sealed class BindingListener : TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if (!string.IsNullOrEmpty(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
