using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
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

namespace DiveCenterManager.Diagnostics;

public sealed partial class AcceptanceRunner
{
    public async Task RunPhase8Async()
    {
        // The existing end-to-end suite covers the complete booking-to-log-to-cash chain,
        // all 15 screens, all 16 reports, sample isolation and an actual schema upgrade.
        await RunAsync();
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var foreignKeys = db.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).ToList();
            Check(foreignKeys.All(x => x.DeleteBehavior == DeleteBehavior.Restrict && x.Properties.All(p => !p.IsShadowProperty())), "EF relationships have no accidental cascades or shadow foreign keys");
            Check(foreignKeys.GroupBy(x => x.DeclaringEntityType.Name + ":" + string.Join(",", x.Properties.Select(p => p.Name))).All(x => x.Count() == 1), "No dependent column is mapped by duplicate relationships");
            Check(db.Model.FindEntityType(typeof(Reservation))!.GetIndexes().Any(x => x.IsUnique && x.Properties.Single().Name == "ReservationNumber")
                && db.Model.FindEntityType(typeof(DiveLog))!.GetIndexes().Any(x => x.IsUnique && x.Properties.Single().Name == "DiveOperationId"), "Reservation numbers and the single shared log per operation remain unique");
            Check(db.Model.FindEntityType(typeof(DiverDiveLog))!.FindProperty("CustomerId")!.IsNullable && db.Model.FindEntityType(typeof(DiveOperation))!.FindProperty("ReservationId")!.IsNullable, "Temporary divers and manual operations retain nullable foreign keys");
            Check(db.Model.GetEntityTypes().SelectMany(x => x.GetProperties()).Where(x => x.ClrType == typeof(decimal) || x.ClrType == typeof(decimal?)).All(x => x.GetColumnType() == "TEXT"), "All money and decimal measurement mappings retain exact decimal text storage");
            Check(!db.Database.HasPendingModelChanges() && !(await db.Database.GetPendingMigrationsAsync()).Any(), "Final EF model and migration history agree");
        }
        await VerifyPhase8PathsAndBackupAsync();
        await VerifyPhase8EditorsAsync();
        await WritePhase8ResultsAsync("phase8-results.json");
    }

    private async Task VerifyPhase8PathsAndBackupAsync()
    {
        var root = Path.Combine(paths.DataDirectory, "stabilization"); Directory.CreateDirectory(root);
        var normalized = new AppPaths(Path.Combine(root, "nested", "..", "normalized"));
        Check(Path.IsPathFullyQualified(normalized.DatabasePath) && normalized.DataDirectory == Path.Combine(root, "normalized") && Directory.GetFiles(normalized.DataDirectory, ".write-check-*").Length == 0, "Central database paths normalize relative segments and leave no write probes behind");
        var collision = Path.Combine(root, "not-a-folder"); await File.WriteAllTextAsync(collision, "preserve");
        await RejectAsync(() => Task.FromResult(new AppPaths(collision)), "A database directory cannot point to a file");
        await RejectAsync(() => Task.FromResult(new AppPaths(" ")), "Blank data folders are rejected");
        var directoryCollision = Path.Combine(root, "database-is-folder"); Directory.CreateDirectory(Path.Combine(directoryCollision, "DiveCenter.db"));
        await RejectAsync(() => Task.FromResult(new AppPaths(directoryCollision)), "A directory named DiveCenter.db is rejected before migrations");
        var readOnly = new AppPaths(Path.Combine(root, "read-only")); await File.WriteAllTextAsync(readOnly.DatabasePath, "preserve");
        File.SetAttributes(readOnly.DatabasePath, FileAttributes.ReadOnly);
        try { await RejectAsync(() => Task.FromResult(new AppPaths(readOnly.DataDirectory)), "Read-only database files produce a clear startup validation error"); }
        finally { File.SetAttributes(readOnly.DatabasePath, FileAttributes.Normal); }
        var designPaths = new AppPaths(Path.Combine(root, "design-time"));
        await using (var design = new DesignTimeDbContextFactory().CreateDbContext(["--data-dir", designPaths.DataDirectory]))
            Check(design.Database.GetDbConnection().ConnectionString == designPaths.ConnectionString, "EF command-line tooling uses the same central path and provider configuration as startup");
        Check(AppOptions.Parse([]) is { SampleData: false, IsDiagnostic: false, Directory: null } && AppOptions.Parse(["--sample-data"]).Directory == Path.Combine(AppPaths.DefaultDataDirectory, "Sample"), "Normal startup never requires sample data; opt-in sample mode selects its own directory");
        foreach (var args in new[] { new[] { "--sample-data", "--data-dir", AppPaths.DefaultDataDirectory }, new[] { "--verify-phase8" }, new[] { "--verify-phase8", "--data-dir", AppPaths.DefaultDataDirectory }, new[] { "--unknown" }, new[] { "--data-dir", "--sample-data" } })
            await RejectAsync(() => Task.FromResult(AppOptions.Parse(args)), "Unsafe or invalid startup options are rejected: " + args[0]);
        var samplePaths = new AppPaths(Path.Combine(root, "sample-settings"), sampleWorkspace: true);
        await using (var sampleServices = new ServiceCollection().AddDiveHub(samplePaths).BuildServiceProvider())
        {
            var settings = sampleServices.GetRequiredService<SettingsViewModel>(); await settings.RefreshAsync();
            Check(settings.Rows.Cast<SettingRow>().Single(x => x.Setting == "Sample business data").Value.StartsWith("Enabled", StringComparison.Ordinal), "Settings identifies an enabled sample workspace accurately");
        }
        var fixture = new AppPaths(Path.Combine(root, "backup-source"));
        await using var fixtureServices = new ServiceCollection().AddDiveHub(fixture).BuildServiceProvider();
        await fixtureServices.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        var backups = fixtureServices.GetRequiredService<BackupService>();
        await using var db = await fixtureServices.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>().CreateDbContextAsync();
        await db.Database.OpenConnectionAsync(); await db.Database.ExecuteSqlRawAsync("PRAGMA wal_autocheckpoint=0;");
        db.Customers.Add(new Customer { FirstName = "Committed", LastName = "WAL" }); await db.SaveChangesAsync();
        var destination = Path.Combine(root, "chosen-folder", "verified.db");
        await backups.CreateAsync(destination);
        Check(File.Exists(fixture.DatabasePath + "-wal") && new FileInfo(fixture.DatabasePath + "-wal").Length > 0, "Backup verification holds a real committed WAL journal open");
        await using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            await backup.OpenAsync(); var command = backup.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM Customers WHERE FirstName='Committed'";
            Check(Convert.ToInt64(await command.ExecuteScalarAsync()) == 1, "Online backup includes committed WAL data and opens as a standalone file");
            command.CommandText = "PRAGMA integrity_check"; Check((string?)await command.ExecuteScalarAsync() == "ok", "Chosen backup passes SQLite integrity verification");
            command.CommandText = "PRAGMA foreign_key_check"; Check(await command.ExecuteScalarAsync() == null, "Backup has no foreign-key violations");
            command.CommandText = "PRAGMA journal_mode"; Check((string?)await command.ExecuteScalarAsync() == "delete", "Published backup uses a standalone journal mode");
        }
        Check(new BackupService(fixture).Folder == Path.GetDirectoryName(destination), "Open Backup Folder remembers the selected folder across service instances");
        await RejectAsync(() => backups.CreateAsync(Path.Combine(fixture.DataDirectory, ".", "DiveCenter.db")), "Normalized backup targets cannot overwrite the live database");
        await RejectAsync(() => backups.CreateAsync(fixture.DatabasePath + "-wal"), "Backup cannot overwrite a live WAL file");
        await RejectAsync(() => backups.CreateAsync(fixture.DataDirectory), "Backup destination must be a file");
        var journalTarget = Path.Combine(root, "journal-target.db"); await File.WriteAllTextAsync(journalTarget + "-wal", "diagnostic journal marker");
        await RejectAsync(() => backups.CreateAsync(journalTarget), "Backup does not replace another database with pending journal files");
        var previousBytes = await File.ReadAllBytesAsync(destination);
        using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { await backups.CreateAsync(destination); throw new InvalidOperationException("Locked backup replacement should fail"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Check(!ErrorMessages.Friendly(e).Contains(" at ", StringComparison.Ordinal), "Locked backup failures have readable UI text without a stack trace"); }
        }
        var preservedBytes = await File.ReadAllBytesAsync(destination);
        Check(previousBytes.SequenceEqual(preservedBytes) && Directory.GetFiles(Path.GetDirectoryName(destination)!, ".divehub-backup-*").Length == 0, "A failed replacement preserves the existing backup and removes temporary files");
        var folderMemo = Path.Combine(fixture.DataDirectory, "last-backup-folder.txt"); File.Delete(folderMemo); Directory.CreateDirectory(folderMemo);
        var metadataFailure = new BackupService(fixture); var secondBackup = Path.Combine(root, "another-folder", "verified.db");
        await metadataFailure.CreateAsync(secondBackup);
        Check(File.Exists(secondBackup) && metadataFailure.Folder == Path.GetDirectoryName(secondBackup) && File.Exists(fixture.LogPath), "A folder-preference write error is logged without reporting a verified backup as failed");
        await using (var invalid = await fixtureServices.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>().CreateDbContextAsync())
        {
            invalid.Currencies.Add(new Currency { Code = "EUR", Name = "Duplicate", Symbol = "E" });
            try { await invalid.SaveChangesAsync(); throw new InvalidOperationException("Unique currency constraint should fail"); }
            catch (DbUpdateException e) { var message = ErrorMessages.Friendly(e); Check(message.Contains("unique", StringComparison.Ordinal) && !message.Contains("SQLite", StringComparison.Ordinal) && !message.Contains("Currencies", StringComparison.Ordinal), "Actual EF constraint errors are translated without exposing SQL/schema details"); }
        }
        foreach (var code in new[] { 5, 6, 8, 10, 11, 13, 14, 26 })
            Check(!ErrorMessages.Friendly(new SqliteException("technical diagnostic detail", code)).Contains("technical diagnostic detail", StringComparison.Ordinal), "SQLite error code has a safe user-facing explanation: " + code);
        var corrupt = new AppPaths(Path.Combine(root, "invalid-database")); var original = "This is not a database"; await File.WriteAllTextAsync(corrupt.DatabasePath, original);
        await using (var corruptServices = new ServiceCollection().AddDiveHub(corrupt).BuildServiceProvider())
        {
            try { await corruptServices.GetRequiredService<DatabaseInitializer>().InitializeAsync(); throw new InvalidOperationException("Invalid database should fail"); }
            catch (SqliteException e)
            {
                await using var source = new FileStream(corrupt.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(source);
                Check(ErrorMessages.Friendly(e).Contains("healthy SQLite", StringComparison.Ordinal) && await reader.ReadToEndAsync() == original, "Invalid startup databases produce a clear error and are preserved");
            }
        }
        var main = services.GetRequiredService<MainViewModel>();
        await main.BackupToAsync(Path.Combine(paths.BackupDirectory, "final-mvp.db"));
        Check(main.HasNotice && !main.NoticeIsError && main.Notice.Contains("Verified", StringComparison.Ordinal), "Backup ViewModel publishes an inline completion message");
        main.DismissNoticeCommand.Execute(null); Check(!main.HasNotice, "Application notices can be dismissed");
    }

    private async Task VerifyPhase8EditorsAsync()
    {
        var masters = services.GetRequiredService<MasterDataService>(); var lookups = services.GetRequiredService<LookupService>();
        var bookings = services.GetRequiredService<ReservationService>(); var operations = services.GetRequiredService<DiveOperationService>();
        var logbook = services.GetRequiredService<LogbookService>(); var cash = services.GetRequiredService<TransactionService>();
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var activity = await db.Activities.Select(x => x.Id).FirstAsync(); var site = await db.DiveSites.Select(x => x.Id).FirstAsync(); var eur = await db.Currencies.Where(x => x.Code == "EUR").Select(x => x.Id).SingleAsync();
        var folder = Path.Combine(paths.DataDirectory, "phase8-screenshots"); Directory.CreateDirectory(folder);
        var listener = new BindingListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            async Task Invalid(object entity, Func<object, Task> save, string name, string? field = null)
            {
                var editor = new EditorViewModel(name, entity, save); await editor.InitializeAsync(lookups, masters); await editor.SaveCommand.ExecuteAsync();
                Check(editor.Error.Length > 0 && !editor.IsSaving && (field == null || editor.Fields.Single(x => x.Key == field).Error.Length > 0), "Inline form validation: " + name);
                await RenderPhase4EditorAsync(editor, folder, name + ".png", false);
            }
            await Invalid(new Customer(), masters.SaveAsync, "Customer-required-names", "FirstName");
            await Invalid(new Customer { FirstName = "Valid", LastName = "Name", Email = "invalid" }, masters.SaveAsync, "Customer-email", "Email");
            await Invalid(new Reservation { Participants = [new() { FirstName = "Test", LastName = "Visitor" }] }, x => bookings.SaveAsync((Reservation)x), "Reservation-activity", "ActivityId");
            var booking = Draft(activity, DateTime.Today); booking.ActivityDate = default;
            await Invalid(booking, x => bookings.SaveAsync((Reservation)x), "Reservation-date");
            await Invalid(new Reservation { ActivityId = activity, NumberOfParticipants = 0 }, x => bookings.SaveAsync((Reservation)x), "Reservation-participants");
            await Invalid(new DiveOperation { Participants = [new() { FirstName = "Test", LastName = "Diver" }] }, x => operations.SaveAsync((DiveOperation)x), "Operation-site", "DiveSiteId");
            await Invalid(new DiveOperation { DiveSiteId = site, Date = default, Participants = [new() { FirstName = "Test", LastName = "Diver" }] }, x => operations.SaveAsync((DiveOperation)x), "Operation-date");
            await Invalid(new DiveOperation { DiveSiteId = site }, x => operations.SaveAsync((DiveOperation)x), "Operation-participants");
            var savedLog = await logbook.GetAsync(await db.DiveLogs.Select(x => x.Id).SingleAsync()); savedLog.MaxDepthMeters = -1;
            await Invalid(savedLog, x => logbook.SaveAsync((DiveLog)x), "DiveLog-depth", "MaxDepthMeters");
            savedLog.MaxDepthMeters = 18; savedLog.ExitTime = savedLog.EntryTime.Subtract(TimeSpan.FromMinutes(1));
            await Invalid(savedLog, x => logbook.SaveAsync((DiveLog)x), "DiveLog-time");
            var diverLog = (await logbook.GetAsync(savedLog.Id)).DiverLogs.First(); diverLog.StartPressureBar = -1;
            await Invalid(diverLog, x => logbook.SavePersonalAsync((DiverDiveLog)x), "DiverLog-pressure", "StartPressureBar");
            diverLog.StartPressureBar = 200; diverLog.GasType = GasType.Nitrox; diverLog.OxygenPercentage = null;
            await Invalid(diverLog, x => logbook.SavePersonalAsync((DiverDiveLog)x), "Nitrox-oxygen-required");
            diverLog.OxygenPercentage = 101;
            await Invalid(diverLog, x => logbook.SavePersonalAsync((DiverDiveLog)x), "Nitrox-oxygen-range", "OxygenPercentage");
            await Invalid(new Transaction { CurrencyId = eur, Amount = 0 }, x => cash.SaveAsync((Transaction)x), "Transaction-amount");
            await Invalid(new Transaction { Amount = 1 }, x => cash.SaveAsync((Transaction)x), "Transaction-currency", "CurrencyId");
            await Invalid(new Boat { Name = "Capacity", Capacity = 0 }, masters.SaveAsync, "Boat-capacity", "Capacity");
            await Invalid(new Equipment { EquipmentType = EquipmentType.Mask }, masters.SaveAsync, "Equipment-identification");
            var equipment = await db.Equipment.AsNoTracking().SingleAsync();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var busy = new EditorViewModel("Save lifecycle", equipment, async x => { await gate.Task; await masters.SaveAsync(x); }); await busy.InitializeAsync(lookups, masters);
            var window = new EditorWindow { DataContext = busy, ShowActivated = false, ShowInTaskbar = false }; window.Show();
            var saving = busy.SaveCommand.ExecuteAsync();
            try
            {
                Check(busy.IsSaving && !busy.CanModify && !busy.CancelCommand.CanExecute(null) && !busy.SaveCommand.CanExecute(null), "Saving disables field changes, repeated saves and cancel");
                window.Close(); Check(window.IsVisible, "A real WPF editor cannot close while persistence is running");
            }
            finally { gate.TrySetResult(); await saving; window.Close(); }
            Check(!busy.IsSaving && busy.CancelCommand.CanExecute(null) && busy.Error.Length == 0, "Successful persistence restores editor state and permits closing");
            var failureEditor = new EditorViewModel("Handled database error", equipment, _ => Task.FromException(new IOException("Technical diagnostic"))); await failureEditor.InitializeAsync(lookups, masters); await failureEditor.SaveCommand.ExecuteAsync();
            Check(!failureEditor.IsSaving && failureEditor.Error.Length > 0 && !failureEditor.Error.Contains("Technical diagnostic", StringComparison.Ordinal), "An asynchronous save error stays visible and leaves the editor usable");
            var reports = services.GetRequiredService<ReportsViewModel>(); reports.ReportField.SetValue(ReportKind.MonthlySummary); await reports.ClearFiltersCommand.ExecuteAsync();
            Check(reports.SelectedKind == ReportKind.MonthlySummary && reports.Message.Length == 0 && reports.Rows.Count == 48, "Shared Clear Filters command preserves the report selection without modifying a collection during enumeration");
            Check(listener.Errors.Count == 0, "All final validation screens and real editor lifecycle have no WPF binding errors");
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "phase8-binding-diagnostics.txt"), listener.Errors); }
    }

    public async Task VerifyPhase8PersistenceAsync()
    {
        await VerifyPersistenceAsync();
        Check(File.Exists(Path.Combine(paths.BackupDirectory, "final-mvp.db")), "Final MVP backup remains available after application restart");
        await WritePhase8ResultsAsync("phase8-persistence-results.json");
    }

    public async Task VerifyStartupAsync(MainWindow shell)
    {
        var listener = new BindingListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        var folder = Path.Combine(paths.DataDirectory, "startup-screenshots"); Directory.CreateDirectory(folder);
        try
        {
            await shell.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var main = (MainViewModel)shell.DataContext; await main.NavigationTask;
            Check(shell.IsLoaded && shell.IsVisible && main.Current is DashboardViewModel && !main.Current.IsBusy, "Real WPF startup shows the shell and loads the dashboard");
            Check(shell.Style == Application.Current.FindResource(typeof(Window)) && shell.FontSize == 14 && shell.Icon != null, "Actual windows apply the shared typography, colors and Dive Hub icon");
            Check(main.Navigation.Count == 11, "The simplified sidebar contains eleven modules");
            var navigation = VisualDescendants(shell).OfType<ListBox>().Single(x => AutomationProperties.GetName(x) == "Main navigation");
            foreach (var item in main.Navigation)
            {
                navigation.SelectedItem = item;
                await shell.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                await main.NavigationTask;
                await shell.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check(main.SelectedNavigation == item && main.Current?.GetType() == item.Create().GetType() && main.Current.Message.Length == 0, "Real sidebar selection loads: " + item.Title);
            }
            shell.Width = 1100; shell.Height = 720;
            await shell.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await CaptureLoadedContentAsync((FrameworkElement)shell.Content, Path.Combine(folder, "Settings-Minimum.png"));
            navigation.SelectedItem = main.Navigation.Single(x => x.Title == "Dashboard"); await shell.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind); await main.NavigationTask;
            await shell.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await CaptureLoadedContentAsync((FrameworkElement)shell.Content, Path.Combine(folder, "Dashboard-Minimum.png"));
            var fileMenu = VisualDescendants(shell).OfType<Menu>().Single().Items.OfType<MenuItem>().Single();
            fileMenu.IsSubmenuOpen = true; await shell.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(fileMenu.Items.OfType<MenuItem>().Any(x => Equals(x.Command, main.BackupCommand)) && fileMenu.Items.OfType<MenuItem>().Any(x => Equals(x.Command, main.OpenBackupFolderCommand)), "File menu connects Backup Database and Open Backup Folder commands");
            fileMenu.IsSubmenuOpen = false;
            Check(listener.Errors.Count == 0 && !main.HasNotice, "Real startup and navigation finish without binding errors or an unhandled-error notice");
            await WritePhase8ResultsAsync("startup-results.json");
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "startup-binding-diagnostics.txt"), listener.Errors); }
    }

    private static async Task CaptureLoadedContentAsync(FrameworkElement content, string path)
    {
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream = File.Create(path); encoder.Save(stream);
    }
    private Task WritePhase8ResultsAsync(string file) => File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, file), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
}
