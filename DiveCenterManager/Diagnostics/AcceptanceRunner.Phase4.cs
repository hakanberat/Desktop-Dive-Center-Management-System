using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    public async Task RunPhase4Async()
    {
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        var reservations = services.GetRequiredService<ReservationService>();
        var operations = services.GetRequiredService<DiveOperationService>();
        var masters = services.GetRequiredService<MasterDataService>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            Check(!await db.Reservations.AnyAsync() && !await db.DiveOperations.AnyAsync(), "PHASE 4 uses a fresh isolated SQLite workspace");
            Check(!db.Database.HasPendingModelChanges(), "Operation model matches the migration snapshot");
            var operationFks = db.Model.FindEntityType(typeof(DiveOperation))!.GetForeignKeys().ToList();
            var participantFks = db.Model.FindEntityType(typeof(DiveOperationParticipant))!.GetForeignKeys().ToList();
            Check(operationFks.Count == 4 && participantFks.Count == 4 && operationFks.Concat(participantFks).All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "Operation and participant foreign keys protect historical records");
        }
        var customer = new Customer { FirstName = "Aylin", LastName = "Deniz" };
        var tryDive = new Activity { Name = "PHASE4 Try Dive", ActivityType = ActivityType.TryDive };
        var guided = new Activity { Name = "PHASE4 Guided", ActivityType = ActivityType.GuidedDive };
        var course = new Activity { Name = "PHASE4 Course", ActivityType = ActivityType.Course };
        var rental = new Activity { Name = "PHASE4 Rental", ActivityType = ActivityType.EquipmentRental };
        var site = new DiveSite { Name = "PHASE4 Shore" }; var alternateSite = new DiveSite { Name = "PHASE4 Reef" };
        var boat = new Boat { Name = "PHASE4 Boat", Capacity = 5 }; var alternateBoat = new Boat { Name = "PHASE4 Second Boat", Capacity = 6 };
        var role = (await services.GetRequiredService<StaffRoleService>().ListAsync()).Single(x => x.Name == "Instructor");
        var instructor = new Staff { FirstName = "Lead", LastName = "Instructor", StaffRoles = [new() { StaffRoleId = role.Id }] };
        var alternateInstructor = new Staff { FirstName = "Other", LastName = "Instructor", StaffRoles = [new() { StaffRoleId = role.Id }] };
        foreach (var record in new object[] { customer, tryDive, guided, course, rental, site, alternateSite, boat, alternateBoat, instructor, alternateInstructor }) await masters.SaveAsync(record);
        var monday = DateTime.Today.AddDays(-((6 + (int)DateTime.Today.DayOfWeek) % 7));
        var day = monday.AddDays(2);
        var source = Draft(tryDive.Id, day); source.StartTime = TimeSpan.FromHours(9.5); source.Status = ReservationStatus.Confirmed;
        source.MainCustomerId = customer.Id; source.InstructorId = instructor.Id; source.DiveSiteId = site.Id; source.BoatId = boat.Id; source.Notes = "Source booking notes";
        source.Participants = [new() { CustomerId = customer.Id, FirstName = customer.FirstName, LastName = customer.LastName }, new() { FirstName = "Temporary", LastName = "Guest", Notes = "Visitor notes" }];
        var later = Draft(guided.Id, day); later.StartTime = TimeSpan.FromHours(11);
        var cancelled = Draft(tryDive.Id, day); cancelled.StartTime = TimeSpan.FromHours(8); cancelled.Status = ReservationStatus.Cancelled;
        var rentalBooking = Draft(rental.Id, day); rentalBooking.StartTime = TimeSpan.FromHours(14);
        var noShow = Draft(tryDive.Id, day); noShow.StartTime = TimeSpan.FromHours(16); noShow.Status = ReservationStatus.NoShow;
        var courseBooking = Draft(course.Id, day.AddDays(1)); courseBooking.DiveSiteId = site.Id;
        foreach (var record in new[] { source, later, cancelled, rentalBooking, noShow, courseBooking }) { record.NumberOfParticipants = record.Participants.Count; await reservations.SaveAsync(record); }

        var main = services.GetRequiredService<MainViewModel>();
        await main.OpenAsync(new NavigationItem("Legacy daily schedule", () => new DailyOperationsViewModel(reservations, services.GetRequiredService<ReservationViewModel>)));
        var daily = (DailyOperationsViewModel)main.Current!;
        await main.OpenAsync(main.Navigation.Single(x => x.Title == "Calendar"));
        var calendar = (CalendarViewModel)main.Current!;
        await main.OpenAsync(new NavigationItem("Legacy operations", services.GetRequiredService<OperationViewModel>));
        var vm = (OperationViewModel)main.Current!; vm.AddLogAction(id => ActivatorUtilities.CreateInstance<LogbookViewModel>(services).CreateFromOperationAsync(id));
        Check(main.Current?.Message.Length == 0, "Calendar and legacy operation compatibility screens resolve");
        var folder = Path.Combine(paths.DataDirectory, "phase4-screenshots"); Directory.CreateDirectory(folder);
        var listener = new BindingListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            await VerifyPhase4ScheduleAsync(daily, calendar, source, day, folder);
            daily.Selected = daily.Rows.Cast<Reservation>().Single(x => x.Id == source.Id);
            var draft = await operations.DraftFromReservationAsync(((Reservation)daily.Selected!).Id);
            Check(draft.Id == 0 && draft.Participants.All(x => x.Id == 0) && draft.ReservationId == source.Id, "Conversion creates an unsaved operation with new participant entities");
            Check(draft.Date == source.ActivityDate && draft.StartTime == source.StartTime && draft.DiveType == ActivityType.TryDive
                && draft.LeadInstructorId == instructor.Id && draft.DiveSiteId == site.Id && draft.BoatId == boat.Id, "Conversion copies date, time, type, instructor, site and boat");
            Check(draft.Participants.Count == 2 && draft.Participants.All(x => x.Role == ParticipantRole.DSDParticipant)
                && draft.Participants.Select(x => x.ReservationParticipantId).SequenceEqual(source.Participants.Select(x => (int?)x.Id)), "Try-dive conversion copies each reservation participant once with its source link");
            Check(draft.Participants[1].CustomerId == null && draft.Participants[1].Notes == "Visitor notes", "Conversion retains temporary participants and their notes");
            Check((await operations.ListAsync()).Count == 0, "Preparing or abandoning a conversion draft does not write an operation");
            var courseDraft = await operations.DraftFromReservationAsync(courseBooking.Id);
            var guidedDraft = await operations.DraftFromReservationAsync(later.Id);
            Check(courseDraft.Participants.All(x => x.Role == ParticipantRole.Student) && guidedDraft.Participants.All(x => x.Role == ParticipantRole.Diver), "Course and guided-dive participant roles are prefilled appropriately");
            await RejectAsync(() => operations.DraftFromReservationAsync(cancelled.Id), "Cancelled bookings cannot be converted");
            await RejectAsync(() => operations.DraftFromReservationAsync(noShow.Id), "No-show bookings cannot be converted");
            await RejectAsync(() => operations.DraftFromReservationAsync(rentalBooking.Id), "Non-diving bookings cannot be converted");

            var editor = await vm.CreateEditorAsync(draft);
            Check(editor.ReferenceValue == source.ReservationNumber && editor.Summary.Contains("Planned"), "Operation form displays the source reservation number and workflow status");
            Check(new[] { "Date", "StartTime", "DiveSiteId", "BoatId", "LeadInstructorId", "DiveType", "Notes" }.All(key => editor.Fields.Any(x => x.Key == key)), "Operation form contains the requested editable fields");
            SetOperationField(editor, "Date", day.AddDays(1)); SetOperationField(editor, "StartTime", TimeSpan.FromHours(10));
            SetOperationField(editor, "DiveSiteId", alternateSite.Id); SetOperationField(editor, "BoatId", alternateBoat.Id);
            SetOperationField(editor, "LeadInstructorId", alternateInstructor.Id); SetOperationField(editor, "DiveType", ActivityType.TrainingDive);
            SetOperationField(editor, "Notes", "Reviewed conversion notes");
            var children = editor.Children!; children.Selected = children.Items[0];
            var personEditor = await children.CreateEditorAsync(false);
            SetOperationField(personEditor, "Role", ParticipantRole.Student); SetOperationField(personEditor, "BuddyGroup", "A");
            SetOperationField(personEditor, "InstructorId", alternateInstructor.Id); SetOperationField(personEditor, "EquipmentRequired", true);
            await personEditor.SaveCommand.ExecuteAsync();
            Check(personEditor.Error.Length == 0, "Converted participant can be edited before saving the operation");
            var duplicate = await children.CreateEditorAsync(true); SetOperationField(duplicate, "CustomerId", customer.Id);
            await duplicate.SaveCommand.ExecuteAsync();
            Check(duplicate.Error.Length > 0 && children.Items.Count == 2, "Operation participant editor rejects duplicate linked customers");
            var extra = await children.CreateEditorAsync(true); SetOperationField(extra, "FirstName", "Extra"); SetOperationField(extra, "LastName", "Visitor");
            SetOperationField(extra, "Role", ParticipantRole.Divemaster); await extra.SaveCommand.ExecuteAsync();
            Check(extra.Error.Length == 0 && children.Items.Count == 3, "Operation participant form adds a manual participant alongside copied participants");
            await editor.SaveCommand.ExecuteAsync();
            Check(editor.Error.Length == 0 && draft.Id > 0 && draft.Participants.All(x => x.Id > 0 && x.DiveOperationId == draft.Id), "Operation Save persists the aggregate and updates generated participant IDs");
            var saved = await operations.GetAsync(draft.Id);
            Check(saved.Date == day.AddDays(1) && saved.StartTime == TimeSpan.FromHours(10) && saved.DiveSiteId == alternateSite.Id
                && saved.BoatId == alternateBoat.Id && saved.LeadInstructorId == alternateInstructor.Id && saved.DiveType == ActivityType.TrainingDive, "All reviewed conversion overrides persist in SQLite");
            Check(saved.Participants[0].Role == ParticipantRole.Student && saved.Participants[0].BuddyGroup == "A" && saved.Participants[0].EquipmentRequired
                && saved.Participants[0].Customer != null && saved.Participants[0].ReservationParticipant != null, "Operation loads participant fields and linked customer/source navigations");
            Check((await reservations.GetAsync(source.Id)).DiveSiteId == site.Id && (await reservations.GetAsync(source.Id)).StartTime == TimeSpan.FromHours(9.5), "Editing the operation draft does not change its source reservation");
            var ids = saved.Participants.Select(x => x.Id).ToArray(); await operations.SaveAsync(draft);
            Check((await operations.GetAsync(draft.Id)).Participants.Select(x => x.Id).SequenceEqual(ids), "Saving the same operation instance twice does not duplicate participants");

            await VerifyPhase4ParticipantsAsync(vm, operations, draft.Id, source, later);
            var manual = await VerifyPhase4ManualAsync(vm, operations, site.Id, day);
            await VerifyPhase4WorkflowAsync(vm, operations, draft.Id, manual.Id);
            await VerifyPhase4OperationQueriesAsync(operations, draft.Id, source.ReservationNumber, day.AddDays(1));
            await operations.SaveAsync(courseDraft);
            var secondCourseDive = await operations.DraftFromReservationAsync(courseBooking.Id); await operations.SaveAsync(secondCourseDive);
            Check(courseDraft.Id != secondCourseDive.Id && courseDraft.Participants[0].Id != secondCourseDive.Participants[0].Id, "A course can intentionally have several distinct operations without sharing participant entities");
            var staleDraft = await operations.DraftFromReservationAsync(later.Id); staleDraft.DiveSiteId = site.Id;
            await reservations.CancelAsync(later.Id);
            await RejectAsync(() => operations.SaveAsync(staleDraft), "Conversion rechecks reservation eligibility when the draft is saved");

            await vm.ClearFiltersCommand.ExecuteAsync(); vm.Selected = vm.Rows.Cast<DiveOperation>().Single(x => x.Id == draft.Id);
            await RenderAsync(new DiveOperationView { DataContext = vm }, Path.Combine(folder, "Dive-Operations.png"), 1200, 850);
            await RenderPhase4EditorAsync(await vm.CreateEditorAsync(await operations.GetAsync(draft.Id)), folder, "Completed-Operation.png", false);
            await RenderPhase4EditorAsync(await vm.CreateEditorAsync(await operations.GetAsync(courseDraft.Id)), folder, "Operation-Participants.png", true);
            Check(listener.Errors.Count == 0, "PHASE 4 schedules, operation views and editors render without binding errors");
            await using var db = await factory.CreateDbContextAsync();
            Check(await db.DiveLogs.CountAsync() == 0, "Completed operations are logbook-ready without automatically creating PHASE 5 records");
            saved = await operations.GetAsync(draft.Id);
            var snapshot = new Phase4Snapshot(saved.Id, manual.Id, saved.ReservationId, saved.Participants.Select(x => x.Id).ToArray(),
                saved.Participants.Select(x => x.ReservationParticipantId).ToArray(), await db.DiveOperations.CountAsync());
            await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase4-expected.json"), JsonSerializer.Serialize(snapshot));
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "phase4-binding-diagnostics.txt"), listener.Errors);
        }
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase4-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task VerifyPhase4ScheduleAsync(DailyOperationsViewModel daily, CalendarViewModel calendar, Reservation source, DateTime day, string folder)
    {
        daily.Filters.Single(x => x.Key == "Date").SetValue(day); await daily.RefreshAsync();
        Check(daily.Rows.Count == 5 && daily.Days.Count == 1 && daily.Rows.Cast<Reservation>().Select(x => x.StartTime).SequenceEqual(daily.Rows.Cast<Reservation>().Select(x => x.StartTime).Order()), "Daily Operations loads only the selected date in time order");
        Check(daily.Rows.Cast<Reservation>().Single(x => x.Id == source.Id) is { NumberOfParticipants: 2, MainCustomer: not null, Instructor: not null, DiveSite: not null, Boat: not null }, "Daily records load participant count, main customer, instructor, site and boat");
        Check(daily.Actions.All(x => !x.Label.Contains("operation", StringComparison.OrdinalIgnoreCase)), "Schedules do not expose the legacy operation workflow");
        daily.Selected = daily.Rows.Cast<Reservation>().Single(x => x.Id == source.Id);
        var detail = daily.CreateSelectedReservationDetail(); await detail.RefreshAsync();
        Check(daily.OpenReservationCommand.CanExecute(null) && detail.Title == source.ReservationNumber, "Daily Operations links to the PHASE 3 reservation detail screen");
        var dailyView = new DailyOperationsView { DataContext = daily }; await RenderAsync(dailyView, Path.Combine(folder, "Daily-Operations.png"), 1150, 1050);
        Check(VisualDescendants(dailyView).OfType<TextBlock>().All(x => !new TextRange(x.ContentStart, x.ContentEnd).Text.Contains("Customer:")), "Schedule cards show no customer fields");

        calendar.Filters.Single(x => x.Key == "Date").SetValue(day); await calendar.RefreshAsync();
        Check(calendar.IsWeekView && calendar.Days.Count == 7 && calendar.Days[0].Date.DayOfWeek == DayOfWeek.Monday && calendar.Rows.Count == 6, "Week view groups the Monday-to-Sunday reservation period");
        Check(calendar.Days.Count(x => x.Reservations.Count == 0) == 5, "Week overview includes empty days");
        await RenderAsync(new CalendarView { DataContext = calendar }, Path.Combine(folder, "Calendar-Week.png"), 1150, 1100);
        await calendar.Days.Single(x => x.Date == day).SelectCommand.ExecuteAsync();
        Check(!calendar.IsWeekView && calendar.Days.Count == 1 && calendar.Rows.Count == 5, "Clicking a week day opens its Day View");
        calendar.Selected = calendar.Rows.Cast<Reservation>().Single(x => x.Id == source.Id);
        var calendarDetail = calendar.CreateSelectedReservationDetail(); await calendarDetail.RefreshAsync();
        Check(calendarDetail.Title == source.ReservationNumber, "Calendar selection resolves the correct reservation detail");
        await RenderAsync(new CalendarView { DataContext = calendar }, Path.Combine(folder, "Calendar-Day.png"), 1150, 1050);
        await ((Commands.AsyncRelayCommand)calendar.Actions.Single(x => x.Label == "Next ›").Command).ExecuteAsync();
        Check(calendar.Days[0].Date == day.AddDays(1) && calendar.Rows.Count == 1, "Calendar next-day navigation changes the queried date");
        calendar.Filters.Single(x => x.Key == "View").SetValue("Week"); calendar.Filters.Single(x => x.Key == "Date").SetValue(day);
        await calendar.RefreshAsync();
        await ((Commands.AsyncRelayCommand)calendar.Actions.Single(x => x.Label == "Next ›").Command).ExecuteAsync();
        Check(calendar.Days[0].Date == day.AddDays(7).AddDays(-2) && calendar.Rows.Count == 0, "Calendar next-week navigation advances seven days");
        daily.Filters.Single(x => x.Key == "Date").SetValue(null); await daily.RefreshCommand.ExecuteAsync();
        Check(daily.Message.Length > 0, "Daily Operations rejects an empty selected date");
        daily.Filters.Single(x => x.Key == "Date").SetValue(day); await daily.RefreshAsync();
        calendar.Filters.Single(x => x.Key == "Date").SetValue(DateTime.MaxValue.Date); await calendar.RefreshAsync();
        Check(calendar.Days.Count is > 0 and <= 7, "Calendar handles the final supported week without date overflow");
    }

    private async Task VerifyPhase4ParticipantsAsync(OperationViewModel vm, DiveOperationService service, int id, Reservation source, Reservation other)
    {
        var edit = await vm.CreateEditorAsync(await service.GetAsync(id)); var children = edit.Children!;
        children.Selected = children.Items.Cast<DiveOperationParticipant>().Single(x => x.ReservationParticipantId == null);
        var removedId = ((DiveOperationParticipant)children.Selected).Id; children.RemoveCommand.Execute(null);
        children.Selected = children.Items[1]; var person = await children.CreateEditorAsync(false);
        SetOperationField(person, "BuddyGroup", "B"); SetOperationField(person, "Notes", "Updated participant notes");
        await person.SaveCommand.ExecuteAsync(); await edit.SaveCommand.ExecuteAsync();
        var saved = await service.GetAsync(id);
        Check(edit.Error.Length == 0 && saved.Participants.Count == 2 && saved.Participants[1].BuddyGroup == "B", "Persisted operation participants can be edited and removed through form commands");
        await using (var db = await services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>().CreateDbContextAsync())
            Check(!await db.DiveOperationParticipants.AnyAsync(x => x.Id == removedId), "Removing an unneeded participant deletes only that operation participant row");
        var duplicateSource = await service.GetAsync(id);
        duplicateSource.Participants.Add(new DiveOperationParticipant { FirstName = "Duplicate", LastName = "Visitor", ReservationParticipantId = source.Participants[1].Id });
        await RejectAsync(() => service.SaveAsync(duplicateSource), "The same temporary reservation participant cannot be copied twice into one operation");
        var duplicateId = await service.GetAsync(id); duplicateId.Participants.Add(duplicateId.Participants[1]);
        await RejectAsync(() => service.SaveAsync(duplicateId), "Repeated operation participant identity is rejected");
        var wrongSource = await service.GetAsync(id);
        wrongSource.Participants.Add(new DiveOperationParticipant { FirstName = "Wrong", LastName = "Source", ReservationParticipantId = other.Participants[0].Id });
        await RejectAsync(() => service.SaveAsync(wrongSource), "Participant source must belong to the operation's reservation");
        var switched = await service.GetAsync(id); switched.Participants[0].ReservationParticipantId = null;
        await RejectAsync(() => service.SaveAsync(switched), "Existing participant source links cannot be silently changed");
        var moved = await service.GetAsync(id); moved.ReservationId = other.Id;
        await RejectAsync(() => service.SaveAsync(moved), "Existing operation source reservation is immutable");
        var invalidRole = await service.GetAsync(id); invalidRole.Participants[0].Role = (ParticipantRole)999;
        await RejectAsync(() => service.SaveAsync(invalidRole), "Invalid operation participant roles are rejected");
        Check((await service.GetAsync(id)).Participants.Count == 2, "Rejected participant changes roll back the whole aggregate");
    }

    private async Task<DiveOperation> VerifyPhase4ManualAsync(OperationViewModel vm, DiveOperationService service, int siteId, DateTime day)
    {
        var operation = new DiveOperation { Date = day }; var editor = await vm.CreateEditorAsync(operation);
        Check(editor.ReferenceValue == "Manual operation" && operation.ReservationId == null, "Manual operation form starts without a reservation link");
        await editor.SaveCommand.ExecuteAsync(); Check(editor.Error.Length > 0 && operation.Id == 0, "Manual operation requires a dive site");
        SetOperationField(editor, "DiveSiteId", siteId); await editor.SaveCommand.ExecuteAsync();
        Check(editor.Error.Contains("participant"), "Manual operation requires at least one participant");
        var person = await editor.Children!.CreateEditorAsync(true);
        SetOperationField(person, "FirstName", "Manual"); SetOperationField(person, "LastName", "Diver"); SetOperationField(person, "Role", ParticipantRole.Instructor);
        await person.SaveCommand.ExecuteAsync(); await editor.SaveCommand.ExecuteAsync();
        Check(editor.Error.Length == 0 && operation.Id > 0 && operation.ReservationId == null && operation.Participants[0].ReservationParticipantId == null, "Manual operation and participant persist without reservation/customer references");
        var missingDate = await service.GetAsync(operation.Id); missingDate.Date = default;
        await RejectAsync(() => service.SaveAsync(missingDate), "Operation service rejects an invalid date");
        var missingTime = await service.GetAsync(operation.Id); missingTime.StartTime = TimeSpan.FromHours(24);
        await RejectAsync(() => service.SaveAsync(missingTime), "Operation service rejects an invalid time");
        return operation;
    }

    private async Task VerifyPhase4WorkflowAsync(OperationViewModel vm, DiveOperationService service, int completedId, int cancelledId)
    {
        await vm.ClearFiltersCommand.ExecuteAsync(); vm.Selected = vm.Rows.Cast<DiveOperation>().Single(x => x.Id == completedId);
        var logAction = vm.Actions.Single(x => x.Label == "Create dive log");
        Check(vm.StartCommand.CanExecute(null) && !vm.CompleteCommand.CanExecute(null) && vm.CancelOperationCommand.CanExecute(null) && !logAction.Command.CanExecute(null), "Planned operation exposes only valid workflow actions");
        await RejectAsync(() => service.ChangeStatusAsync(completedId, OperationStatus.Completed), "An operation cannot skip directly from Planned to Completed");
        await vm.StartCommand.ExecuteAsync();
        Check(vm.Message.Length == 0 && ((DiveOperation)vm.Selected!).Status == OperationStatus.InProgress && vm.CompleteCommand.CanExecute(null) && !vm.StartCommand.CanExecute(null), "Start command moves Planned to InProgress and updates action availability");
        await vm.CompleteCommand.ExecuteAsync();
        Check(vm.Message.Length == 0 && ((DiveOperation)vm.Selected!).Status == OperationStatus.Completed && !vm.CancelOperationCommand.CanExecute(null) && logAction.Command.CanExecute(null), "Complete command moves InProgress to Completed and enables the existing logbook handoff");
        var closed = await vm.CreateEditorAsync(await service.GetAsync(completedId));
        Check(!closed.CanEdit && !closed.SaveCommand.CanExecute(null) && !closed.Children!.AddCommand.CanExecute(null), "Completed operations and participants open read-only");
        await RejectAsync(() => service.ChangeStatusAsync(completedId, OperationStatus.InProgress), "Completed operations cannot move backwards");
        await RejectAsync(() => service.SaveAsync((DiveOperation)closed.Entity), "Service protects completed operation edits");
        await service.ChangeStatusAsync(cancelledId, OperationStatus.Cancelled);
        var cancelled = await service.GetAsync(cancelledId);
        Check(cancelled.Status == OperationStatus.Cancelled && cancelled.Participants.Count == 1, "Cancelling a planned operation retains its participant records");
        await RejectAsync(() => service.ChangeStatusAsync(cancelledId, OperationStatus.InProgress), "Cancelled operations cannot restart");
    }

    private async Task VerifyPhase4OperationQueriesAsync(DiveOperationService service, int id, string reservationNumber, DateTime day)
    {
        foreach (var search in new[] { reservationNumber, "Aylin Deniz", "PHASE4 Reef", "Other Instructor", "Reviewed conversion notes" })
            Check((await service.ListAsync(search: " " + search + " ")).Any(x => x.Id == id), "Operation search includes booking, participants, site, team or notes: " + search);
        Check((await service.ListAsync(day, day, OperationStatus.Completed)).Any(x => x.Id == id), "Operation date and status filters combine");
        Check(!(await service.ListAsync(day, day, OperationStatus.Planned)).Any(x => x.Id == id), "Operation status filter excludes nonmatching records");
        Check((await service.ListAsync(to: DateTime.MaxValue)).Any(x => x.Id == id), "Operation maximum end date does not overflow");
        await RejectAsync(() => service.ListAsync(day.AddDays(1), day), "Operation reversed date range is rejected");
    }

    private static void SetOperationField(EditorViewModel editor, string key, object? value) => editor.Fields.Single(x => x.Key == key).SetValue(value);

    private static async Task RenderPhase4EditorAsync(EditorViewModel editor, string folder, string filename, bool participants)
    {
        var window = new EditorWindow { DataContext = editor }; var content = (FrameworkElement)window.Content;
        window.Content = null; content.DataContext = editor;
        if (participants) VisualDescendants(content).OfType<TabControl>().Single().SelectedIndex = 1;
        await RenderAsync(content, Path.Combine(folder, filename), 880, 810);
    }

    public sealed record Phase4Snapshot(int CompletedId, int CancelledId, int? ReservationId, int[] ParticipantIds, int?[] SourceIds, int OperationCount);

    public async Task VerifyPhase4PersistenceAsync()
    {
        var expected = JsonSerializer.Deserialize<Phase4Snapshot>(await File.ReadAllTextAsync(Path.Combine(paths.DataDirectory, "phase4-expected.json")))!;
        var service = services.GetRequiredService<DiveOperationService>();
        var completed = await service.GetAsync(expected.CompletedId); var cancelled = await service.GetAsync(expected.CancelledId);
        Check(completed.Status == OperationStatus.Completed && cancelled.Status == OperationStatus.Cancelled, "Restart preserves completed and cancelled operation states");
        Check(completed.ReservationId == expected.ReservationId && completed.Participants.Select(x => x.Id).SequenceEqual(expected.ParticipantIds)
            && completed.Participants.Select(x => x.ReservationParticipantId).SequenceEqual(expected.SourceIds), "Restart preserves operation participant identities and reservation source links");
        Check(completed.Participants.Any(x => x.Customer != null) && completed.Participants.Any(x => x.CustomerId == null), "Restart reloads both customer-linked and temporary participants");
        await using var db = await services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>().CreateDbContextAsync();
        Check(await db.DiveOperations.CountAsync() == expected.OperationCount && !(await db.Database.GetPendingMigrationsAsync()).Any(), "Restart retains all operation rows with no pending migrations");
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase4-persistence-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
