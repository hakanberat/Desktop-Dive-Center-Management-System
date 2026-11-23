using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DiveCenterManager.Commands;
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
    // Focused PHASE 2 checks run through the production editors, commands and SQLite services.
    public async Task RunPhase2Async()
    {
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            Check(!await db.Customers.AnyAsync() && !await db.Activities.AnyAsync() && !await db.Reservations.AnyAsync()
                && !await db.Transactions.AnyAsync(), "PHASE 2 checks require a fresh isolated database");
            Check(!db.Database.HasPendingModelChanges(), "PHASE 2 preserves the migration snapshot");
        }

        var listener = new BindingListener();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            var currency = await VerifyCatalogAsync<Currency, CurrencyViewModel>(
                new() { ["Code"] = "CHF", ["Name"] = "Swiss franc", ["Symbol"] = "Fr" }, "Name", "PHASE2 Swiss franc");
            var activity = await VerifyCatalogAsync<Activity, ActivityViewModel>(
                new() { ["Name"] = "PHASE2 Try dive", ["ActivityType"] = ActivityType.TryDive, ["DefaultPrice"] = 123.45m, ["DefaultCurrencyId"] = currency.Id }, "Notes", "PHASE2 activity notes");
            var customer = await VerifyCatalogAsync<Customer, CustomerViewModel>(
                new() { ["FirstName"] = "Aylin", ["LastName"] = "Deniz", ["Email"] = "aylin@example.test", ["TotalLoggedDives"] = 12, ["LastDiveDate"] = DateTime.Today.AddYears(-1) }, "GeneralNotes", "PHASE2 customer notes");
            var site = await VerifyCatalogAsync<DiveSite, DiveSiteViewModel>(
                new() { ["Name"] = "PHASE2 Shore", ["Location"] = "Test coast", ["Latitude"] = 35.1m, ["Longitude"] = 33.9m, ["MaximumDepthMeters"] = 18m }, "Notes", "PHASE2 site notes");
            var roleService = services.GetRequiredService<StaffRoleService>();
            var instructorRole = (await roleService.ListAsync()).Single(x => x.Name == "Instructor");
            var captainRole = (await roleService.ListAsync()).Single(x => x.Name == "Captain");
            var staff = await VerifyCatalogAsync<Staff, StaffViewModel>(
                new() { ["FirstName"] = "PHASE2", ["LastName"] = "Instructor", ["Email"] = "staff@example.test" }, "Notes", "PHASE2 staff notes",
                editor => { editor.Roles.Single(x => x.Id == instructorRole.Id).IsSelected = true; editor.Roles.Single(x => x.Id == captainRole.Id).IsSelected = true; });
            var boat = await VerifyCatalogAsync<Boat, BoatViewModel>(
                new() { ["Name"] = "PHASE2 Boat", ["Capacity"] = 12, ["RegistrationNumber"] = "TEST-02" }, "Notes", "PHASE2 boat notes");
            var equipment = await VerifyCatalogAsync<Equipment, EquipmentViewModel>(
                new() { ["EquipmentType"] = EquipmentType.Regulator, ["Brand"] = "Test", ["SerialNumber"] = "PHASE2-001", ["Status"] = EquipmentStatus.Available }, "Notes", "PHASE2 equipment notes");
            var category = await VerifyCatalogAsync<TransactionCategory, TransactionCategoryViewModel>(
                new() { ["Name"] = "PHASE2 Category", ["Type"] = CategoryType.Both }, "Name", "PHASE2 edited category");

            await VerifyPhase2RulesAsync(customer, activity, site, staff, boat, equipment, currency, category, instructorRole, captainRole);
            await VerifyPhase2FiltersAsync(activity, staff, boat, equipment, category, instructorRole.Id);
            var main = services.GetRequiredService<MainViewModel>();
            foreach (var (title, type) in new (string, Type)[]
            {
                ("Activities / Courses", typeof(ActivityViewModel)),
                ("Dive Sites", typeof(DiveSiteViewModel)), ("Staff", typeof(StaffViewModel)),
                ("Boats", typeof(BoatViewModel)), ("Equipment", typeof(EquipmentViewModel))
            })
            {
                await main.OpenAsync(main.Navigation.Single(x => x.Title == title));
                Check(main.Current?.GetType() == type && main.Current.Message.Length == 0, "Sidebar resolves the typed module: " + title);
            }
            var settings = services.GetRequiredService<SettingsViewModel>();
            Check(settings.Actions.Any(x => x.Label == "Currencies") && settings.Actions.Any(x => x.Label == "Transaction Categories"), "Settings exposes currencies and transaction categories");
            Check(listener.Errors.Count == 0, "All eight PHASE 2 views and editors render without binding errors");
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "phase2-binding-diagnostics.txt"), listener.Errors);
        }
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase2-results.json"),
            JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed, database = paths.DatabasePath }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<T> VerifyCatalogAsync<T, TViewModel>(Dictionary<string, object?> values, string editField, string editedValue, Action<EditorViewModel>? configure = null)
        where T : class, IActiveEntity, new() where TViewModel : CatalogViewModel<T>
    {
        var name = typeof(T).Name;
        var vm = services.GetRequiredService<TViewModel>();
        var editor = await vm.CreateEditorAsync(true);
        foreach (var (key, value) in values) editor.Fields.Single(x => x.Key == key).SetValue(value);
        configure?.Invoke(editor);
        var closed = false;
        editor.CloseRequested += result => closed = result;
        await editor.SaveCommand.ExecuteAsync();
        var record = (T)editor.Entity;
        Check(closed && record.Id > 0 && editor.Error.Length == 0, name + ": form Save command creates a SQLite record");

        await vm.RefreshAsync();
        vm.Selected = vm.Rows.Cast<T>().Single(x => x.Id == record.Id);
        var cancelled = await vm.CreateEditorAsync(false);
        cancelled.Fields.Single(x => x.Key == editField).SetValue("Cancelled draft");
        cancelled.CancelCommand.Execute(null);
        Check(!Equals(typeof(T).GetProperty(editField)!.GetValue(vm.Selected), "Cancelled draft"), name + ": cancelling edits leaves the selected record unchanged");
        editor = await vm.CreateEditorAsync(false);
        editor.Fields.Single(x => x.Key == editField).SetValue(editedValue);
        await editor.SaveCommand.ExecuteAsync();
        Check(editor.Error.Length == 0, name + ": form Save command updates successfully");
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var saved = await db.Set<T>().AsNoTracking().SingleAsync(x => x.Id == record.Id);
            Check(Equals(typeof(T).GetProperty(editField)!.GetValue(saved), editedValue), name + ": a new DbContext reads the edited value from SQLite");
        }

        vm.Filters.Single(x => x.Key == "Search").Text = editedValue;
        await vm.RefreshAsync();
        Check(vm.Rows.Count == 1 && ((T)vm.Rows[0]).Id == record.Id, name + ": search finds the saved record");
        vm.Selected = vm.Rows[0];
        await ((AsyncRelayCommand)vm.Actions.Single(x => x.Label == "Deactivate").Command).ExecuteAsync();
        Check(vm.Message.Length == 0 && vm.Rows.Count == 0, name + ": Deactivate removes the record from the active list");
        vm.Filters.Single(x => x.Key == "ShowInactive").Checked = true;
        await vm.RefreshAsync();
        vm.Selected = vm.Rows.Cast<T>().Single(x => x.Id == record.Id);
        Check(!((T)vm.Selected).IsActive, name + ": Include inactive retrieves the preserved record");
        await ((AsyncRelayCommand)vm.Actions.Single(x => x.Label == "Activate").Command).ExecuteAsync();
        Check(vm.Message.Length == 0 && vm.Rows.Cast<T>().Single(x => x.Id == record.Id).IsActive, name + ": Activate restores the record");
        await vm.ClearFiltersCommand.ExecuteAsync();
        vm.Selected = vm.Rows.Cast<T>().Single(x => x.Id == record.Id);

        var template = (DataTemplate)Application.Current.FindResource(new DataTemplateKey(typeof(TViewModel)));
        var view = (FrameworkElement)template.LoadContent();
        view.DataContext = vm;
        Check(view.GetType().Name == name + "View", name + ": module-specific WPF View resolves");
        var folder = Path.Combine(paths.DataDirectory, "phase2-screenshots");
        Directory.CreateDirectory(folder);
        await RenderAsync(view, Path.Combine(folder, name + ".png"), 1120, 820);
        editor = await vm.CreateEditorAsync(false);
        var window = new EditorWindow { DataContext = editor };
        var content = (FrameworkElement)window.Content; window.Content = null; content.DataContext = editor;
        await RenderAsync(content, Path.Combine(folder, name + "-Editor.png"), 880, 780);
        return (T)vm.Selected;
    }

    private async Task VerifyPhase2RulesAsync(Customer customer, Activity activity, DiveSite site, Staff staff, Boat boat, Equipment equipment,
        Currency currency, TransactionCategory category, StaffRole instructorRole, StaffRole captainRole)
    {
        var customers = services.GetRequiredService<CustomerService>();
        var activities = services.GetRequiredService<ActivityService>();
        var sites = services.GetRequiredService<DiveSiteService>();
        var people = services.GetRequiredService<StaffService>();
        var boats = services.GetRequiredService<BoatService>();
        var gear = services.GetRequiredService<EquipmentService>();
        var currencies = services.GetRequiredService<CurrencyService>();
        var categories = services.GetRequiredService<TransactionCategoryService>();
        var roles = services.GetRequiredService<StaffRoleService>();
        var lookups = services.GetRequiredService<LookupService>();
        var masters = services.GetRequiredService<MasterDataService>();
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();

        Check(customer.CertificationAgency == null && customer.TotalLoggedDives == 12 && customer.PriorLoggedDives == 12, "Uncertified customer and imported dive count persist");
        Check(activity.DefaultPrice == 123.45m && activity.DefaultCurrency?.Code == "CHF", "Activity price stays decimal and loads its related currency");
        Check(staff.StaffRoles.Count == 2, "Staff form persists multiple roles");
        staff.StaffRoles.RemoveAll(x => x.StaffRoleId == captainRole.Id);
        await people.SaveAsync(staff);
        Check((await people.ListAsync()).Single(x => x.Id == staff.Id).StaffRoles.Count == 1, "Staff editing removes only the deselected role link");
        await roles.SetActiveAsync(captainRole.Id, false);
        await RejectAsync(() => people.SaveAsync(new Staff { FirstName = "Invalid", LastName = "Role", StaffRoles = [new() { StaffRoleId = captainRole.Id }] }), "New assignments cannot use inactive staff roles");
        await roles.SetActiveAsync(instructorRole.Id, false);
        await people.SaveAsync(staff);
        Check((await people.ListAsync()).Single(x => x.Id == staff.Id).StaffRoles.Count == 1, "Existing inactive staff role can be retained while editing");
        await roles.SetActiveAsync(instructorRole.Id, true);
        await RejectAsync(() => currencies.SaveAsync(new Currency { Code = "chf", Name = "Duplicate", Symbol = "Fr" }), "Currency code uniqueness is case-normalized");
        await RejectAsync(() => activities.SaveAsync(new Activity { Name = "No currency", DefaultPrice = 10m }), "Activity default price requires a currency");
        await RejectAsync(() => sites.SaveAsync(new DiveSite { Name = "Invalid", Latitude = 91 }), "Dive-site latitude is validated");
        await RejectAsync(() => sites.SaveAsync(new DiveSite { Name = "Invalid", MaximumDepthMeters = -1 }), "Dive-site depth cannot be negative");
        await RejectAsync(() => boats.SaveAsync(new Boat { Name = "Invalid", Capacity = 0 }), "Boat capacity must be positive");
        await RejectAsync(() => gear.SaveAsync(new Equipment { EquipmentType = (EquipmentType)999 }), "Invalid catalog enum values are rejected");
        await RejectAsync(() => people.SaveAsync(new Staff { FirstName = "Invalid", LastName = "Email", Email = "invalid" }), "Staff email is validated");
        await RejectAsync(() => customers.SaveAsync(new Customer { FirstName = "Future", LastName = "Birth", BirthDate = DateTime.Today.AddDays(1) }), "Customer birth date cannot be in the future");
        var customerVm = services.GetRequiredService<CustomerViewModel>();
        var invalidEditor = await customerVm.CreateEditorAsync(true);
        await invalidEditor.SaveCommand.ExecuteAsync();
        Check(invalidEditor.Error.Length > 0 && invalidEditor.Fields.Single(x => x.Key == "FirstName").Error.Length > 0, "Required customer fields show visible editor errors");
        try { await boats.SaveAsync(new Boat { Id = int.MaxValue, Name = "Deleted elsewhere" }); throw new Exception("Missing record was recreated"); }
        catch (System.ComponentModel.DataAnnotations.ValidationException) { Check(true, "Editing a missing catalog record does not silently insert it"); }

        Check(equipment.Status == EquipmentStatus.Maintenance, "Reactivated equipment is consistently active and in Maintenance");
        equipment.Status = EquipmentStatus.Retired; await gear.SaveAsync(equipment);
        Check(!(await gear.ListAsync()).Single(x => x.Id == equipment.Id).IsActive, "Selecting Retired also deactivates equipment");
        await gear.SetActiveAsync(equipment.Id, true);

        // Historical fixtures exercise PHASE 2 preservation rules; no later-phase UI is developed here.
        await using (var db = await factory.CreateDbContextAsync())
        {
            var reservation = new Reservation { ReservationNumber = "PHASE2-HISTORY", ActivityId = activity.Id, MainCustomerId = customer.Id, InstructorId = staff.Id, DiveSiteId = site.Id, BoatId = boat.Id };
            var operation = new DiveOperation { Reservation = reservation, DiveSiteId = site.Id, BoatId = boat.Id, LeadInstructorId = staff.Id, Status = OperationStatus.Completed };
            var log = new DiveLog { DiveOperation = operation, DiveSiteId = site.Id, MaxDepthMeters = 12m, BottomTimeMinutes = 40 };
            db.DiverDiveLogs.Add(new DiverDiveLog { DiveLog = log, CustomerId = customer.Id, ParticipantName = customer.FullName });
            db.Transactions.AddRange(
                new Transaction { CurrencyId = currency.Id, CategoryId = category.Id, ActivityId = activity.Id, Reservation = reservation, TransactionType = TransactionType.Income, Amount = 123.45m },
                new Transaction { CurrencyId = currency.Id, CategoryId = category.Id, TransactionType = TransactionType.Expense, Amount = 12.34m });
            var savedCustomer = await db.Customers.SingleAsync(x => x.Id == customer.Id);
            savedCustomer.TotalLoggedDives = 13; savedCustomer.LastDiveDate = DateTime.Today;
            await db.SaveChangesAsync();
        }
        category.Type = CategoryType.Income;
        await RejectAsync(() => categories.SaveAsync(category), "Category type cannot conflict with existing expense history");
        category.Type = CategoryType.Expense;
        await RejectAsync(() => categories.SaveAsync(category), "Category type cannot conflict with existing income history");
        category.Type = CategoryType.Both;
        currency.Code = "CAD";
        await RejectAsync(() => currencies.SaveAsync(currency), "Currency with cash history cannot be renamed");
        currency.Code = "CHF";
        customer = (await customers.ListAsync()).Single(x => x.Id == customer.Id);
        customer.TotalLoggedDives = 0;
        await RejectAsync(() => customers.SaveAsync(customer), "Customer total cannot fall below locally recorded dives");
        customer.TotalLoggedDives = 13; await customers.SaveAsync(customer);
        Check(customer.PriorLoggedDives == 12, "Editing a profile preserves prior dives alongside local logs");
        foreach (var record in new IActiveEntity[] { customer, activity, site, staff, boat, currency, category }) await masters.SetActiveAsync(record, false);
        var history = await customers.HistoryAsync(customer.Id);
        Check(!history.Customer.IsActive && history.Reservations.Count == 1 && history.Dives.Count == 1, "Customer reservation and dive history survive master deactivation");
        foreach (var (key, id) in new (string, int)[] { ("CustomerId", customer.Id), ("ActivityId", activity.Id), ("DiveSiteId", site.Id), ("InstructorId", staff.Id), ("BoatId", boat.Id), ("CurrencyId", currency.Id), ("CategoryId", category.Id) })
        {
            Check(!(await lookups.GetAsync(key)).Any(x => Equals(x.Value, id)) && (await lookups.GetAsync(key, id)).Any(x => Equals(x.Value, id)), "Lookup excludes new inactive assignments but preserves the current selection: " + key);
        }
        activity.Notes = "Edited while currency is inactive"; await activities.SaveAsync(activity);
        await RejectAsync(() => activities.SaveAsync(new Activity { Name = "New inactive reference", DefaultCurrencyId = currency.Id }), "New activity cannot select an inactive currency");
        await using (var db = await factory.CreateDbContextAsync())
            Check(await db.Transactions.CountAsync() == 2 && await db.DiveLogs.CountAsync() == 1 && await db.Reservations.CountAsync() == 1, "All historical rows and foreign keys survive catalog deactivation");
        foreach (var record in new IActiveEntity[] { customer, activity, site, staff, boat, currency, category }) await masters.SetActiveAsync(record, true);
    }

    private async Task VerifyPhase2FiltersAsync(Activity activity, Staff staff, Boat boat, Equipment equipment, TransactionCategory category, int instructorRoleId)
    {
        var activities = services.GetRequiredService<ActivityViewModel>();
        activities.Filters.Single(x => x.Key == "ActivityType").SelectedValue = ActivityType.Course;
        await activities.RefreshAsync(); Check(activities.Rows.Count == 0, "Activity type filter excludes nonmatching records");
        activities.Filters.Single(x => x.Key == "ActivityType").SelectedValue = activity.ActivityType;
        await activities.RefreshAsync(); Check(activities.Rows.Count == 1, "Activity type filter finds matching records");
        var people = services.GetRequiredService<StaffViewModel>();
        people.Filters.Single(x => x.Key == "StaffRole").SelectedValue = instructorRoleId;
        await people.RefreshAsync(); Check(people.Rows.Cast<Staff>().Any(x => x.Id == staff.Id), "Staff role filter queries persisted role links");
        var boats = services.GetRequiredService<BoatViewModel>();
        boats.Filters.Single(x => x.Key == "Capacity").Text = (boat.Capacity + 1).ToString();
        await boats.RefreshAsync(); Check(boats.Rows.Count == 0, "Minimum boat capacity filter works");
        var gear = services.GetRequiredService<EquipmentViewModel>();
        gear.Filters.Single(x => x.Key == "EquipmentType").SelectedValue = equipment.EquipmentType;
        gear.Filters.Single(x => x.Key == "Status").SelectedValue = EquipmentStatus.Maintenance;
        await gear.RefreshAsync(); Check(gear.Rows.Cast<Equipment>().Any(x => x.Id == equipment.Id), "Equipment type and status filters combine");
        var categories = services.GetRequiredService<TransactionCategoryViewModel>();
        categories.Filters.Single(x => x.Key == "CategoryType").SelectedValue = CategoryType.Both;
        categories.Filters.Single(x => x.Key == "Search").Text = category.Name;
        await categories.RefreshAsync(); Check(categories.Rows.Count == 1, "Category type and text filters combine");
    }
}
