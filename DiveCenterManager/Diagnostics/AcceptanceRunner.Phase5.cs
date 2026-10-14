using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DiveCenterManager.Commands;
using DiveCenterManager.Data;
using DiveCenterManager.Helpers;
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
    public async Task RunPhase5Async()
    {
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        var masters = services.GetRequiredService<MasterDataService>();
        var reservations = services.GetRequiredService<ReservationService>();
        var operations = services.GetRequiredService<DiveOperationService>();
        var logbook = services.GetRequiredService<LogbookService>();
        var customers = services.GetRequiredService<CustomerService>();
        var main = services.GetRequiredService<MainViewModel>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            Check(!await db.DiveLogs.AnyAsync() && !await db.Customers.AnyAsync(), "PHASE 5 runs on fresh isolated SQLite without business seed data");
            Check(!db.Database.HasPendingModelChanges(), "Logbook model matches the applied migrations");
            var shared = db.Model.FindEntityType(typeof(DiveLog))!;
            var personal = db.Model.FindEntityType(typeof(DiverDiveLog))!;
            Check(shared.GetIndexes().Any(x => x.IsUnique && x.Properties.Single().Name == "DiveOperationId"), "SQLite model enforces one shared log per operation");
            Check(shared.GetForeignKeys().Count() == 2 && personal.GetForeignKeys().Count() == 4
                && shared.GetForeignKeys().Concat(personal.GetForeignKeys()).All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "Shared and personal log foreign keys protect the domain chain");
            Check(new[] { "DiveSiteId", "Date", "EntryTime", "ExitTime", "WaterTemperatureCelsius", "VisibilityMeters", "WeatherCondition" }.All(x => personal.FindProperty(x) == null), "Shared measurements are not duplicated in personal storage");
        }
        var day = DateTime.Today.AddDays(-2);
        var diver = new Customer { FirstName = "Aylin", LastName = "Deniz", TotalLoggedDives = 7, LastDiveDate = day.AddYears(-1) };
        var buddy = new Customer { FirstName = "Emre", LastName = "Kaya" };
        var replacement = new Customer { FirstName = "Selin", LastName = "Ada" };
        var site = new DiveSite { Name = "PHASE5 Reef" };
        var activity = new Activity { Name = "PHASE5 Guided dive", ActivityType = ActivityType.GuidedDive };
        var role = (await services.GetRequiredService<StaffRoleService>().ListAsync()).Single(x => x.Name == "Instructor");
        var instructor = new Staff { FirstName = "Lead", LastName = "Instructor", StaffRoles = [new() { StaffRoleId = role.Id }] };
        foreach (var item in new object[] { diver, buddy, replacement, site, activity, instructor }) await masters.SaveAsync(item);
        var reservation = Draft(activity.Id, day); reservation.DiveSiteId = site.Id; reservation.InstructorId = instructor.Id;
        reservation.Participants = [new() { CustomerId = diver.Id, FirstName = diver.FirstName, LastName = diver.LastName },
            new() { CustomerId = buddy.Id, FirstName = buddy.FirstName, LastName = buddy.LastName }, new() { FirstName = "Temporary", LastName = "Visitor" }];
        await reservations.SaveAsync(reservation);
        var operation = await operations.DraftFromReservationAsync(reservation.Id); await operations.SaveAsync(operation);
        await RejectAsync(() => logbook.DraftFromOperationAsync(operation.Id), "Planned operation cannot prepare a log");
        await RejectAsync(() => logbook.SaveAsync(new DiveLog { DiveOperationId = operation.Id, DiveSiteId = site.Id, DiverLogs = [new() { ParticipantName = "Invalid source" }] }), "Save independently rejects a non-completed source");
        await operations.ChangeStatusAsync(operation.Id, OperationStatus.InProgress);
        await RejectAsync(() => logbook.DraftFromOperationAsync(operation.Id), "In-progress operation cannot prepare a log");
        await operations.ChangeStatusAsync(operation.Id, OperationStatus.Completed);
        var draft = await logbook.DraftFromOperationAsync(operation.Id);
        var concurrentDraft = await logbook.DraftFromOperationAsync(operation.Id);
        Check(draft.Id == 0 && draft.DiveOperationId == operation.Id && draft.DiveSiteId == site.Id && draft.Date == day && draft.EntryTime == operation.StartTime && draft.DiveType == operation.DiveType, "Completed operation prefills the shared source fields without saving");
        Check(draft.DiverLogs.Count == 3 && draft.DiverLogs.All(x => x.Id == 0 && x.InstructorId == instructor.Id && x.GasType == GasType.Air)
            && draft.DiverLogs.Count(x => x.CustomerId == null) == 1 && draft.DiverLogs[2].ParticipantName == "Temporary Visitor", "Every operation participant prepares one new personal row, including temporary divers");
        Check(!ReferenceEquals(draft.DiverLogs[0], concurrentDraft.DiverLogs[0]) && (await logbook.ListAsync()).Count == 0, "Separate drafts do not share entities or write database records");
        Check(draft.BottomTimeMinutes == 0 && draft.ExitTime == draft.EntryTime, "New drafts do not invent actual dive duration or wrap past midnight");
        await main.OpenAsync(new NavigationItem("Legacy logbook", () => ActivatorUtilities.CreateInstance<LogbookViewModel>(services)));
        var vm = (LogbookViewModel)main.Current!;
        var editor = await vm.CreateEditorAsync(draft);
        Check(editor.ReferenceValue.Contains(operation.Id.ToString()) && editor.Fields.All(x => x.Key != "DiveOperationId"), "Log editor shows an immutable completed-operation reference");
        Check(new[] { "DiveSiteId", "Date", "EntryTime", "ExitTime", "BottomTimeMinutes", "MaxDepthMeters", "WaterTemperatureCelsius", "VisibilityMeters", "CurrentCondition", "WeatherCondition", "DiveType", "Notes" }.All(key => editor.Fields.Any(x => x.Key == key)), "Shared editor exposes every requested shared field");
        SetOperationField(editor, "EntryTime", TimeSpan.FromHours(9)); SetOperationField(editor, "ExitTime", TimeSpan.FromHours(10));
        SetOperationField(editor, "BottomTimeMinutes", 45); SetOperationField(editor, "MaxDepthMeters", 22.5m);
        SetOperationField(editor, "WaterTemperatureCelsius", 25m); SetOperationField(editor, "VisibilityMeters", 18m);
        SetOperationField(editor, "CurrentCondition", "Mild"); SetOperationField(editor, "WeatherCondition", "Clear"); SetOperationField(editor, "Notes", "Shared reef observations");
        var children = editor.Children!; children.Selected = children.Items.Cast<DiverDiveLog>().Single(x => x.CustomerId == diver.Id);
        var personEditor = await children.CreateEditorAsync(false);
        Check(new[] { "CustomerId", "ParticipantName", "InstructorId", "BuddyCustomerId", "MaxDepthMeters", "BottomTimeMinutes", "StartPressureBar", "EndPressureBar", "TankSizeLiters", "GasType", "OxygenPercentage", "WeightKg", "SuitType", "PersonalNotes", "IsVerified" }.All(key => personEditor.Fields.Any(x => x.Key == key)), "Personal editor exposes every requested personal field");
        SetOperationField(personEditor, "BuddyCustomerId", buddy.Id); SetOperationField(personEditor, "GasType", GasType.Nitrox);
        SetOperationField(personEditor, "OxygenPercentage", 32m); SetOperationField(personEditor, "StartPressureBar", 210m); SetOperationField(personEditor, "EndPressureBar", 65m);
        SetOperationField(personEditor, "TankSizeLiters", 12m); SetOperationField(personEditor, "WeightKg", 4m);
        SetOperationField(personEditor, "SuitType", "5 mm wetsuit"); SetOperationField(personEditor, "PersonalNotes", "Personal observation");
        SetOperationField(personEditor, "IsVerified", true); await personEditor.SaveCommand.ExecuteAsync();
        Check(personEditor.Error.Length == 0, "Personal fields save into the pending aggregate through the real WPF command");
        var duplicateEditor = await children.CreateEditorAsync(true); SetOperationField(duplicateEditor, "CustomerId", diver.Id);
        await duplicateEditor.SaveCommand.ExecuteAsync();
        Check(duplicateEditor.Error.Length > 0 && children.Items.Count == 3, "Personal editor rejects a duplicate linked customer before save");
        await editor.SaveCommand.ExecuteAsync();
        Check(editor.Error.Length == 0 && draft.Id > 0 && draft.DiverLogs.All(x => x.Id > 0 && x.DiveLogId == draft.Id), "Aggregate Save commits shared/personal data and propagates generated IDs");
        var ids = draft.DiverLogs.Select(x => x.Id).ToArray(); await logbook.SaveAsync(draft);
        Check((await logbook.GetAsync(draft.Id)).DiverLogs.Select(x => x.Id).SequenceEqual(ids), "Saving the same instance twice preserves personal row identities");
        await RejectAsync(() => logbook.SaveAsync(concurrentDraft), "A stale second draft cannot create a duplicate shared log");
        Check((await logbook.DraftFromOperationAsync(operation.Id)).Id == draft.Id, "Creating again from a logged operation reopens its existing log");
        await using (var db = await factory.CreateDbContextAsync())
        {
            var duplicate = (DiveLog)EntityCopy.Copy(draft); duplicate.Id = 0; db.DiveLogs.Add(duplicate);
            try { await db.SaveChangesAsync(); throw new InvalidOperationException("Missing unique database constraint"); }
            catch (DbUpdateException) { Check(true, "Actual SQLite unique index rejects duplicate logs even when the service is bypassed"); }
        }
        var saved = await logbook.GetAsync(draft.Id);
        Check(saved.DiveOperation?.ReservationId == reservation.Id && saved.DiveSite != null && saved.DiverLogs[0].Customer != null
            && saved.DiverLogs[0].Instructor != null && saved.DiverLogs[0].BuddyCustomer != null, "Detail loads the complete domain chain and personal reference navigations");
        Check(saved.MaxDepthMeters == 22.5m && saved.BottomTimeMinutes == 45 && saved.Notes == "Shared reef observations"
            && saved.DiverLogs[0].MaxDepthMeters == null && saved.DiverLogs[0].BottomTimeMinutes == null, "Common depth/time remain shared when personal overrides are blank");
        var history = await customers.HistoryAsync(diver.Id);
        Check(history.Dives.Single() is { MaxDepthMeters: 22.5m, BottomTimeMinutes: 45, GasType: GasType.Nitrox, Verified: true }
            && history.Dives[0].Buddy == buddy.FullName && history.Dives[0].Instructor == instructor.FullName
            && history.Customer.TotalLoggedDives == 8 && history.Customer.LastDiveDate == day, "Customer history resolves shared values, buddy, instructor and prior-plus-local totals");
        var detail = vm.CreateDetail(draft.Id); await detail.RefreshAsync();
        var selectedId = saved.DiverLogs.Single(x => x.CustomerId == diver.Id).Id;
        var individual = await detail.CreatePersonalEditorAsync(selectedId);
        SetOperationField(individual, "MaxDepthMeters", 20m); SetOperationField(individual, "BottomTimeMinutes", 40);
        await individual.SaveCommand.ExecuteAsync();
        Check(individual.Error.Length == 0 && (await customers.HistoryAsync(diver.Id)).Dives[0] is { MaxDepthMeters: 20m, BottomTimeMinutes: 40 }, "Detail edits personal overrides and history uses them");
        Check((await logbook.GetAsync(draft.Id)) is { MaxDepthMeters: 22.5m, BottomTimeMinutes: 45 }, "Saving one diver does not overwrite shared measurements");
        await detail.RefreshAsync(); detail.Selected = detail.Rows.Cast<DiverDiveLog>().Single(x => x.Id == selectedId);
        await ((AsyncRelayCommand)detail.Actions.Single(x => x.Label == "Clear verification").Command).ExecuteAsync();
        Check(!(await customers.HistoryAsync(diver.Id)).Dives[0].Verified, "Clear verification action persists through the detail UI command");
        await ((AsyncRelayCommand)detail.Actions.Single(x => x.Label == "Verify selected diver").Command).ExecuteAsync();
        Check((await customers.HistoryAsync(diver.Id)).Dives[0].Verified, "Verify action persists and retains row selection");

        await VerifyPhase5ValidationAsync(logbook, draft.Id, selectedId, buddy.Id);
        var relink = (await logbook.GetAsync(draft.Id)).DiverLogs.Single(x => x.Id == selectedId); relink.CustomerId = replacement.Id;
        await logbook.SavePersonalAsync(relink);
        Check((await customers.HistoryAsync(diver.Id)).Customer.TotalLoggedDives == 7 && (await customers.HistoryAsync(replacement.Id)).Customer.TotalLoggedDives == 1, "Relinking a personal row recalculates both old and new customer totals");
        relink.CustomerId = diver.Id; await logbook.SavePersonalAsync(relink);
        var editAgain = await vm.CreateEditorAsync(await logbook.GetAsync(draft.Id));
        var list = editAgain.Children!; list.Selected = list.Items.Cast<DiverDiveLog>().Single(x => x.CustomerId == buddy.Id); list.RemoveCommand.Execute(null);
        await editAgain.SaveCommand.ExecuteAsync();
        Check(editAgain.Error.Length == 0 && (await customers.HistoryAsync(buddy.Id)).Customer.TotalLoggedDives == 0, "Removing a diver log through the UI updates history transactionally");
        var addAgain = await list.CreateEditorAsync(true); SetOperationField(addAgain, "CustomerId", buddy.Id);
        await addAgain.SaveCommand.ExecuteAsync(); await editAgain.SaveCommand.ExecuteAsync();
        Check(addAgain.Error.Length == 0 && editAgain.Error.Length == 0 && (await customers.HistoryAsync(buddy.Id)).Dives.Count == 1, "Adding a diver log through the UI persists a linked customer snapshot");
        var late = new DiveOperation { DiveSiteId = site.Id, Date = day.AddDays(-1), StartTime = TimeSpan.FromHours(23.75), Participants = [new() { FirstName = "Night", LastName = "Diver" }] };
        await operations.SaveAsync(late); await operations.ChangeStatusAsync(late.Id, OperationStatus.InProgress); await operations.ChangeStatusAsync(late.Id, OperationStatus.Completed);
        var lateDraft = await logbook.DraftFromOperationAsync(late.Id); Services.Validation.Check(lateDraft);
        Check(lateDraft.ExitTime >= lateDraft.EntryTime, "Late operation draft remains valid without implicitly rolling into the next day");
        await logbook.SaveAsync(lateDraft);
        var foreign = await logbook.GetAsync(draft.Id); foreign.DiverLogs.Add(lateDraft.DiverLogs[0]);
        await RejectAsync(() => logbook.SaveAsync(foreign), "Personal rows from another dive cannot be grafted onto the aggregate");
        var moving = await logbook.GetAsync(draft.Id); moving.DiveOperationId = late.Id;
        await RejectAsync(() => logbook.SaveAsync(moving), "An existing log cannot be reassigned to another operation");
        Check((await logbook.ListAsync(day, day, site.Id)).Single().Id == draft.Id && (await logbook.ListAsync(to: DateTime.MaxValue)).Count == 2, "Date/site filters work and maximum end date cannot overflow");
        await RejectAsync(() => logbook.ListAsync(day.AddDays(1), day), "Reversed logbook date range is rejected");
        await logbook.SetVerifiedAsync(draft.Id, selectedId, true);

        var folder = Path.Combine(paths.DataDirectory, "phase5-screenshots"); Directory.CreateDirectory(folder);
        var listener = new BindingListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            await main.OpenAsync(new NavigationItem("Legacy logbook", () => ActivatorUtilities.CreateInstance<LogbookViewModel>(services))); vm = (LogbookViewModel)main.Current!;
            Check(vm.Message.Length == 0 && vm.Rows.Count == 2 && vm.Columns.Any(x => x.Path == "DiverLogs.Count") && vm.Columns.Any(x => x.Path == "DiveOperationId"), "Sidebar opens a populated logbook with operation and participant columns");
            await RenderAsync(new ContentControl { Content = vm }, Path.Combine(folder, "Logbook.png"), 1200, 820);
            detail = vm.CreateDetail(draft.Id); await detail.RefreshAsync(); detail.Selected = detail.Rows.Cast<DiverDiveLog>().Single(x => x.Id == selectedId);
            await RenderAsync(new ContentControl { Content = detail }, Path.Combine(folder, "Dive-Detail.png"), 1200, 900);
            Check(detail.Details.Count == 12 && detail.Rows.Count == 3, "Dedicated detail displays shared information above all diver logs");
            await RenderPhase4EditorAsync(await vm.CreateEditorAsync(await logbook.GetAsync(draft.Id)), folder, "Shared-Editor.png", false);
            await RenderPhase4EditorAsync(await vm.CreateEditorAsync(await logbook.GetAsync(draft.Id)), folder, "Diver-Grid.png", true);
            var personalEditor = await detail.CreatePersonalEditorAsync(selectedId);
            await RenderPhase4EditorAsync(personalEditor, folder, "Personal-Editor.png", false);
            var historyVm = new CustomerHistoryViewModel(diver, customers); await historyVm.RefreshAsync();
            await RenderAsync(new ScreenView { DataContext = historyVm }, Path.Combine(folder, "Customer-History.png"), 1200, 820);
            Check(historyVm.Columns.Any(x => x.Path == "Buddy") && historyVm.Rows.Count == 1, "Customer dive history UI includes buddy and actual stored dives");
            Check(listener.Errors.Count == 0, "Logbook templates, details, editors and history render without WPF binding errors");
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "phase5-binding-diagnostics.txt"), listener.Errors);
        }
        saved = await logbook.GetAsync(draft.Id);
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase5-expected.json"), JsonSerializer.Serialize(new Phase5Snapshot(saved.Id, operation.Id, reservation.Id, diver.Id, buddy.Id, selectedId, saved.DiverLogs.Select(x => x.Id).ToArray())));
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase5-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task VerifyPhase5ValidationAsync(LogbookService logbook, int id, int personalId, int duplicateCustomerId)
    {
        foreach (var (name, mutate) in new (string, Action<DiveLog>)[]
        {
            ("negative shared depth", x => x.MaxDepthMeters = -1), ("negative shared time", x => x.BottomTimeMinutes = -1),
            ("exit before entry", x => x.ExitTime = x.EntryTime - TimeSpan.FromMinutes(1)),
            ("bottom time exceeding duration", x => x.BottomTimeMinutes = 61), ("invalid date", x => x.Date = default),
            ("out-of-range clock time", x => x.EntryTime = TimeSpan.FromHours(25)), ("empty diver list", x => x.DiverLogs.Clear()),
            ("duplicate same temporary entity", x => x.DiverLogs.Add(x.DiverLogs.Single(p => !p.CustomerId.HasValue))),
            ("duplicate temporary identity", x => x.DiverLogs.Add((DiverDiveLog)EntityCopy.Copy(x.DiverLogs.Single(p => !p.CustomerId.HasValue))))
        })
        { var invalid = await logbook.GetAsync(id); mutate(invalid); await RejectAsync(() => logbook.SaveAsync(invalid), "Reject " + name); }
        foreach (var (name, mutate) in new (string, Action<DiverDiveLog>)[]
        {
            ("negative personal depth", x => x.MaxDepthMeters = -1), ("negative personal time", x => x.BottomTimeMinutes = -1),
            ("negative start pressure", x => x.StartPressureBar = -1), ("negative end pressure", x => x.EndPressureBar = -1),
            ("zero tank size", x => x.TankSizeLiters = 0), ("negative tank size", x => x.TankSizeLiters = -1),
            ("negative weight", x => x.WeightKg = -1), ("oxygen below 21", x => x.OxygenPercentage = 20),
            ("oxygen above 100", x => x.OxygenPercentage = 101), ("Nitrox without oxygen", x => x.OxygenPercentage = null),
            ("invalid gas enum", x => x.GasType = (GasType)99), ("empty participant name", x => x.ParticipantName = " "),
            ("self buddy", x => x.BuddyCustomerId = x.CustomerId), ("duplicate customer", x => { x.CustomerId = duplicateCustomerId; x.BuddyCustomerId = null; }),
            ("foreign parent ID", x => x.DiveLogId = int.MaxValue)
        })
        { var invalid = (await logbook.GetAsync(id)).DiverLogs.Single(x => x.Id == personalId); mutate(invalid); await RejectAsync(() => logbook.SavePersonalAsync(invalid), "Reject " + name); }
        foreach (var oxygen in new[] { 21m, 100m })
        {
            var valid = (await logbook.GetAsync(id)).DiverLogs.Single(x => x.Id == personalId); valid.OxygenPercentage = oxygen;
            valid.MaxDepthMeters = 0; valid.BottomTimeMinutes = 0; valid.StartPressureBar = 0; valid.EndPressureBar = 0; valid.WeightKg = 0;
            await logbook.SavePersonalAsync(valid);
            Check((await logbook.GetAsync(id)).DiverLogs.Single(x => x.Id == personalId).OxygenPercentage == oxygen, "Accept zero measurements and Nitrox boundary " + oxygen);
        }
        var restore = (await logbook.GetAsync(id)).DiverLogs.Single(x => x.Id == personalId);
        restore.MaxDepthMeters = 20; restore.BottomTimeMinutes = 40; restore.StartPressureBar = 210; restore.EndPressureBar = 65; restore.WeightKg = 4; restore.OxygenPercentage = 32;
        await logbook.SavePersonalAsync(restore);
    }

    public sealed record Phase5Snapshot(int LogId, int OperationId, int ReservationId, int CustomerId, int BuddyId, int PersonalId, int[] PersonalIds);

    public async Task VerifyPhase5PersistenceAsync()
    {
        var expected = JsonSerializer.Deserialize<Phase5Snapshot>(await File.ReadAllTextAsync(Path.Combine(paths.DataDirectory, "phase5-expected.json")))!;
        var logbook = services.GetRequiredService<LogbookService>();
        var saved = await logbook.GetAsync(expected.LogId);
        Check(saved.DiveOperationId == expected.OperationId && saved.DiveOperation?.ReservationId == expected.ReservationId, "Restart preserves Reservation -> Operation -> DiveLog");
        Check(saved.DiverLogs.Select(x => x.Id).SequenceEqual(expected.PersonalIds) && saved.DiverLogs.All(x => x.DiveLogId == saved.Id), "Restart preserves personal IDs and their shared parent");
        var history = await services.GetRequiredService<CustomerService>().HistoryAsync(expected.CustomerId);
        Check(history.Customer.TotalLoggedDives == 8 && history.Dives.Single() is { MaxDepthMeters: 20, BottomTimeMinutes: 40, GasType: GasType.Nitrox, Verified: true }
            && saved.DiverLogs.Single(x => x.Id == expected.PersonalId).BuddyCustomerId == expected.BuddyId, "Restart preserves verification, personal values, buddy and customer totals");
        Check((await logbook.DraftFromOperationAsync(expected.OperationId)).Id == expected.LogId && (await logbook.ListAsync()).Count == 2, "Restart conversion opens the existing log without duplication");
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase5-persistence-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
