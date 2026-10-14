using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DiveCenterManager.Data;
using DiveCenterManager.Models;
using DiveCenterManager.Services;
using DiveCenterManager.ViewModels;
using DiveCenterManager.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Activity = DiveCenterManager.Models.Activity;

namespace DiveCenterManager.Diagnostics;

public sealed partial class AcceptanceRunner
{
    public async Task RunPhase3Async()
    {
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        var reservations = services.GetRequiredService<ReservationService>();
        var masters = services.GetRequiredService<MasterDataService>();
        var vm = services.GetRequiredService<ReservationViewModel>();
        var day = DateTime.Today;
        await using (var db = await factory.CreateDbContextAsync())
        {
            Check(!await db.Reservations.AnyAsync() && !await db.Customers.AnyAsync(), "Reservation checks use an isolated empty database");
            Check(!db.Database.HasPendingModelChanges(), "Count-only reservations need no migration and preserve the existing schema");
        }
        var activity = new Activity { Name = "Count-only booking", ActivityType = ActivityType.TryDive };
        var boat = new Boat { Name = "Four seats", Capacity = 4 };
        await masters.SaveAsync(activity); await masters.SaveAsync(boat);
        var folder = Path.Combine(paths.DataDirectory, "phase3-screenshots"); Directory.CreateDirectory(folder);
        var listener = new BindingListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            var editor = await vm.CreateEditorAsync(null);
            Check(editor.Children == null && editor.Fields.All(x => x.Key != "MainCustomerId"), "Booking form requests no customer or individual participant information");
            var count = editor.Fields.Single(x => x.Key == "NumberOfParticipants");
            Check(count.Kind == "Choice" && count.IsRequired && Equals(count.SelectedValue, 1) && count.Choices.Count == 10000, "Participant count defaults to one and supports the existing 1-10000 range");
            Set(editor, "NumberOfParticipants", null);
            await editor.SaveCommand.ExecuteAsync();
            Check(editor.Error.Length > 0 && count.Error.Length > 0, "A cleared participant dropdown has visible validation");
            Set(editor, "NumberOfParticipants", 3);
            await editor.SaveCommand.ExecuteAsync();
            Check(editor.Fields.Single(x => x.Key == "ActivityId").Error.Length > 0, "Activity remains required");
            Set(editor, "ActivityId", activity.Id); Set(editor, "BoatId", boat.Id); Set(editor, "Status", ReservationStatus.Confirmed);
            Set(editor, "Notes", "Count-only test");
            Set(editor, "NumberOfParticipants", 5);
            await editor.SaveCommand.ExecuteAsync();
            Check(editor.Error.Contains("capacity", StringComparison.OrdinalIgnoreCase) && ((Reservation)editor.Entity).Id == 0, "Boat capacity validates the selected count even without participant rows");
            Set(editor, "NumberOfParticipants", 3);
            Set(editor, "StartTime", "25:30"); await editor.SaveCommand.ExecuteAsync();
            Check(editor.Error.Length > 0, "Invalid reservation time is rejected");
            Set(editor, "StartTime", TimeSpan.FromHours(9.5));
            await VerifyReservationDateInputAsync(editor, folder);

            editor.Error = "";
            var window = new EditorWindow { DataContext = editor };
            var content = (FrameworkElement)window.Content; window.Content = null; content.DataContext = editor;
            await RenderAsync(content, Path.Combine(folder, "Reservation-New.png"), 880, 810);
            var combo = VisualDescendants(content).OfType<ComboBox>().Single(x => ReferenceEquals(x.DataContext, count));
            Check(!combo.IsEditable && combo.Items.Count == 10000, "Actual WPF participant input is a non-editable dropdown");
            combo.SelectedItem = count.Choices.Single(x => Equals(x.Value, 3));
            await editor.SaveCommand.ExecuteAsync();
            var booking = (Reservation)editor.Entity;
            Check(editor.Error.Length == 0 && booking.Id > 0 && booking.NumberOfParticipants == 3 && booking.Participants.Count == 0 && booking.MainCustomerId == null, "Dropdown-only reservation saves without names or invented customer records");
            Check(booking.ReservationNumber == $"RES-{day.Year:D4}-000001", "Failed saves do not consume the automatic reservation number");
            await using (var db = await factory.CreateDbContextAsync())
                Check(!await db.Customers.AnyAsync() && !await db.ReservationParticipants.AnyAsync(), "Saving a headcount creates no customer or participant records");

            var edit = await vm.CreateEditorAsync(booking.Id);
            Check(Equals(edit.Fields.Single(x => x.Key == "NumberOfParticipants").SelectedValue, 3), "Reopening selects the saved count");
            Set(edit, "NumberOfParticipants", 4); edit.CancelCommand.Execute(null);
            Check((await reservations.GetAsync(booking.Id)).NumberOfParticipants == 3, "Cancelling an edit leaves the saved count unchanged");
            edit = await vm.CreateEditorAsync(booking.Id); Set(edit, "NumberOfParticipants", 4);
            await edit.SaveCommand.ExecuteAsync();
            booking = await reservations.GetAsync(booking.Id);
            Check(edit.Error.Length == 0 && booking.NumberOfParticipants == 4 && booking.Participants.Count == 0, "Editing persists the count independently of participant rows");
            foreach (var invalid in new[] { 0, -1, 10001 })
            {
                var invalidBooking = new Reservation { ActivityId = activity.Id, NumberOfParticipants = invalid };
                await RejectAsync(() => reservations.SaveAsync(invalidBooking), "Service rejects count " + invalid);
            }
            var upperBound = new Reservation { ActivityId = activity.Id, NumberOfParticipants = 10000, ActivityDate = day.AddDays(7) };
            await reservations.SaveAsync(upperBound);
            Check((await reservations.GetAsync(upperBound.Id)).NumberOfParticipants == 10000, "Maximum supported count round-trips without person rows");

            var datedEditor = await vm.CreateEditorAsync(null, day.AddDays(2));
            Check(datedEditor.Fields.Single(x => x.Key == "ActivityDate").Date == day.AddDays(2), "Calendar creation prefills the selected date");
            var main = services.GetRequiredService<MainViewModel>();
            Check(main.Navigation.Select(x => x.Title).SequenceEqual(new[] { "Dashboard", "Reservations", "Calendar", "Activities / Courses", "Dive Sites", "Staff", "Boats", "Equipment", "Transactions", "Reports", "Settings" }), "Sidebar exposes only the simplified modules");
            var calendar = (CalendarViewModel)main.Navigation.Single(x => x.Title == "Calendar").Create();
            calendar.Filters.Single(x => x.Key == "View").SetValue("Day");
            await calendar.RefreshAsync();
            Check(calendar.Rows.Count == 1 && calendar.Cards.Single(x => x.Label == "Participants").Value == "4", "Calendar daily total uses the saved headcount");
            Check(calendar.Actions.All(x => !x.Label.Contains("operation", StringComparison.OrdinalIgnoreCase)), "Calendar has no link to the removed operation workflow");
            calendar.Filters.Single(x => x.Key == "View").SetValue("Week"); await calendar.RefreshAsync();
            Check(calendar.Days.Count == 7 && calendar.Days.Single(x => x.Date == day).Summary.Contains("4 participants"), "Calendar retains weekly planning and accurate per-day counts");
            await RenderAsync(new CalendarView { DataContext = calendar }, Path.Combine(folder, "Calendar.png"), 1150, 1000);
            var dashboard = services.GetRequiredService<DashboardService>();
            var snapshot = await dashboard.GetAsync(day);
            Check(snapshot.TodayReservations == 1 && snapshot.PendingReservations == 1 && snapshot.Participants == 4 && snapshot.Upcoming.Single().Participants == 4, "Dashboard counts anonymous bookings and their selected headcount");
            var reports = services.GetRequiredService<ReportService>();
            foreach (var kind in new[] { ReportKind.ParticipantCounts, ReportKind.ReservationsByActivity })
                Check((await reports.RunAsync(new(kind, day, day))).Cast<OperationalReportRow>().Single().Participants == 4, "Report uses headcount: " + kind);
            Check((await reports.RunAsync(new(ReportKind.ReservationsByDate, day, day))).Cast<ReservationReportRow>().Single().Participants == 4, "Detailed reservation report uses headcount");
            var reportsVm = services.GetRequiredService<ReportsViewModel>();
            Check(reportsVm.ReportField.Choices.Count == 11 && reportsVm.VisibleFilters.All(x => x.Key != "CustomerId"), "Reports expose booking and finance reports without customer filters");

            var paymentVm = services.GetRequiredService<TransactionViewModel>();
            var paymentEditor = await paymentVm.CreatePaymentEditorAsync(booking);
            Check(((Transaction)paymentEditor.Entity).PersonCount == 4, "Reservation payment defaults to the selected participant count");
            await using (var db = await factory.CreateDbContextAsync())
                Set(paymentEditor, "CurrencyId", await db.Currencies.Where(x => x.Code == "EUR").Select(x => x.Id).SingleAsync());
            Set(paymentEditor, "Amount", 120m); await paymentEditor.SaveCommand.ExecuteAsync();
            Check(paymentEditor.Error.Length == 0, "Count-only reservations accept linked cash payments");
            var detail = vm.CreateDetail(booking.Id); await detail.RefreshAsync();
            Check(detail.Details.Single(x => x.Label == "Participants").Value == "4" && detail.Details.All(x => x.Label != "Main customer") && detail.Payments.Count == 1, "Details show participant count and payments without customer fields");
            await RenderAsync(new ReservationDetailView { DataContext = detail }, Path.Combine(folder, "Reservation-Detail.png"), 1150, 1000);

            booking.Status = ReservationStatus.Completed; await reservations.SaveAsync(booking);
            snapshot = await dashboard.GetAsync(day);
            Check(snapshot.CompletedReservations == 1 && snapshot.PendingReservations == 0 && snapshot.RecentReservations.Single().Id == booking.Id && snapshot.Upcoming.Count == 0, "Completing a reservation updates dashboard history without an operation or log");
            await reservations.CancelAsync(booking.Id); await reservations.CancelAsync(booking.Id);
            var cancelled = await reservations.GetAsync(booking.Id);
            Check(cancelled.NumberOfParticipants == 4 && (await reservations.PaymentsAsync(booking.Id)).Count == 1, "Cancellation preserves headcount and linked payments");
            snapshot = await dashboard.GetAsync(day);
            Check(snapshot.Participants == 0 && snapshot.TodayReservations == 0, "Cancelled bookings do not inflate dashboard totals");
            booking = await reservations.GetAsync(booking.Id); booking.Status = ReservationStatus.NoShow; await reservations.SaveAsync(booking);
            Check((await reports.RunAsync(new(ReportKind.ParticipantCounts, day, day))).Cast<OperationalReportRow>().Single().Participants == 0, "No-shows do not inflate participant reports");
            booking.Status = ReservationStatus.Completed; await reservations.SaveAsync(booking);

            var customer = new Customer { FirstName = "Existing", LastName = "Customer" }; await masters.SaveAsync(customer);
            var site = new DiveSite { Name = "Legacy site" }; await masters.SaveAsync(site);
            var legacy = Draft(activity.Id, day.AddDays(-1)); legacy.MainCustomerId = customer.Id;
            legacy.Participants[0].CustomerId = customer.Id; legacy.Participants[0].Notes = "Historical record";
            await reservations.SaveAsync(legacy);
            var personId = legacy.Participants[0].Id;
            await using (var db = await factory.CreateDbContextAsync())
            {
                foreach (var status in new[] { OperationStatus.Planned, OperationStatus.InProgress, OperationStatus.Completed })
                    db.DiveOperations.Add(new DiveOperation { ReservationId = legacy.Id, DiveSiteId = site.Id, Status = status, Participants = [new() { ReservationParticipantId = personId, FirstName = "Existing", LastName = "Customer" }] });
                await db.SaveChangesAsync();
            }
            var legacyEditor = await vm.CreateEditorAsync(legacy.Id); Set(legacyEditor, "NumberOfParticipants", 6); await legacyEditor.SaveCommand.ExecuteAsync();
            legacy = await reservations.GetAsync(legacy.Id);
            Check(legacyEditor.Error.Length == 0 && legacy.NumberOfParticipants == 6 && legacy.MainCustomerId == customer.Id && legacy.Participants.Single().Id == personId && legacy.Participants[0].Notes == "Historical record", "Editing the new count preserves historical people and customer links");
            await reservations.CancelAsync(legacy.Id);
            await using (var db = await factory.CreateDbContextAsync())
                Check(await db.DiveOperations.CountAsync(x => x.ReservationId == legacy.Id && x.Status == OperationStatus.Cancelled) == 2 && await db.DiveOperations.CountAsync(x => x.ReservationId == legacy.Id && x.Status == OperationStatus.Completed) == 1, "Cancellation closes unfinished legacy operations and preserves completed history");

            await vm.ClearFiltersCommand.ExecuteAsync();
            await RenderAsync(new ReservationView { DataContext = vm }, Path.Combine(folder, "Reservation-List.png"), 1150, 850);
            var dashVm = services.GetRequiredService<DashboardViewModel>(); await dashVm.RefreshAsync();
            await RenderAsync(new DashboardView { DataContext = dashVm }, Path.Combine(folder, "Dashboard.png"), 1150, 1150);
            Check(listener.Errors.Count == 0, "Simplified reservation, detail, calendar and dashboard render without binding errors");
            await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase3-expected.json"), JsonSerializer.Serialize(new Phase3Snapshot(booking.Id, booking.ReservationNumber, legacy.Id, personId)));
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "phase3-binding-diagnostics.txt"), listener.Errors);
        }
        await VerifySavedRecordVisibilityAsync();
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase3-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
        static void Set(EditorViewModel target, string key, object? value) => target.Fields.Single(x => x.Key == key).SetValue(value);
    }

    private async Task VerifyReservationDateInputAsync(EditorViewModel editor, string folder)
    {
        var window = new EditorWindow { DataContext = editor };
        var content = (FrameworkElement)window.Content; window.Content = null; content.DataContext = editor;
        await RenderAsync(content, Path.Combine(folder, "Reservation-Date.png"), 880, 810);
        var picker = VisualDescendants(content).OfType<ValidatedDatePicker>().Single(x => x.DataContext is FieldViewModel { Key: "ActivityDate" });
        var input = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);
        var field = (FieldViewModel)picker.DataContext;
        var original = field.Date;
        input.Text = "not-a-date"; await editor.SaveCommand.ExecuteAsync();
        Check(field.InputError.Length > 0 && editor.Error.Length > 0 && ((Reservation)editor.Entity).Id == 0, "Invalid text typed into the WPF DatePicker blocks saving");
        input.Text = original!.Value.ToString("d", CultureInfo.CurrentCulture); field.SetValue(original);
        Check(field.InputError.Length == 0, "Correcting the date clears its validation error");
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in VisualDescendants(child)) yield return nested;
        }
    }

    // Named people remain useful only as fixtures for legacy-data compatibility tests.
    private static Reservation Draft(int activityId, DateTime date) => new()
    {
        ActivityId = activityId, ReservationDate = date, ActivityDate = date,
        Participants = [new ReservationParticipant { FirstName = "Test", LastName = "Visitor" }]
    };
    public sealed record Phase3Snapshot(int Id, string Number, int LegacyId, int LegacyParticipantId);
    public async Task VerifyPhase3PersistenceAsync()
    {
        var expected = JsonSerializer.Deserialize<Phase3Snapshot>(await File.ReadAllTextAsync(Path.Combine(paths.DataDirectory, "phase3-expected.json")))!;
        var service = services.GetRequiredService<ReservationService>();
        var saved = await service.GetAsync(expected.Id);
        Check(saved.ReservationNumber == expected.Number && saved.NumberOfParticipants == 4 && saved.Participants.Count == 0 && saved.MainCustomerId == null && saved.Status == ReservationStatus.Completed, "Restart preserves count-only reservation and completed status");
        Check((await service.PaymentsAsync(saved.Id)).Single().Amount == 120m, "Restart preserves linked payments");
        var legacy = await service.GetAsync(expected.LegacyId);
        Check(legacy.NumberOfParticipants == 6 && legacy.Participants.Single().Id == expected.LegacyParticipantId && legacy.MainCustomerId.HasValue, "Restart retains the updated headcount and historical people independently");
        await using var db = await services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>().CreateDbContextAsync();
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Restart needs no migration");
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase3-persistence-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
