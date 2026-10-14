using System.IO;
using DiveCenterManager.Data;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;
using DiveCenterManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DiveCenterManager.Diagnostics;

public sealed partial class AcceptanceRunner
{
    private async Task VerifySavedRecordVisibilityAsync()
    {
        var isolated = new AppPaths(Path.Combine(paths.DataDirectory, "record-visibility"));
        await using var provider = new ServiceCollection().AddDiveHub(isolated).BuildServiceProvider();
        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        static void Set(EditorViewModel editor, string key, object? value) => editor.Fields.Single(x => x.Key == key).SetValue(value);
        static FieldViewModel Filter(ScreenViewModel vm, string key) => vm.Filters.Single(x => x.Key == key);
        static bool SelectedRecord(ScreenViewModel vm, EditorViewModel editor) => vm.Selected is IEntity selected
            && selected.Id == ((IEntity)editor.Entity).Id && vm.Rows.Contains(vm.Selected);

        var activities = provider.GetRequiredService<ActivityViewModel>();
        await activities.RefreshAsync();
        var activityEditor = await activities.CreateEditorAsync(true);
        Set(activityEditor, "Name", "Visible activity");
        await activityEditor.SaveCommand.ExecuteAsync();
        Check(activityEditor.Error.Length == 0 && SelectedRecord(activities, activityEditor), "Catalog save reloads and selects its record without closing the dialog or manual refresh");

        var bookings = provider.GetRequiredService<ReservationViewModel>();
        await bookings.RefreshAsync();
        var bookingEditor = await bookings.CreateEditorAsync(null, DateTime.Today.AddDays(-1));
        Set(bookingEditor, "ActivityId", ((Activity)activityEditor.Entity).Id);
        var visibleBeforeClose = false;
        bookingEditor.CloseRequested += saved => visibleBeforeClose = saved && SelectedRecord(bookings, bookingEditor);
        await bookingEditor.SaveCommand.ExecuteAsync();
        Check(bookingEditor.Error.Length == 0 && visibleBeforeClose && Filter(bookings, "From").Date == null,
            "Past reservation is reloaded and selected before editor close; hiding date filter is cleared");

        Filter(bookings, "Search").Text = "Visible activity";
        var editedBooking = await bookings.CreateEditorAsync(((Reservation)bookingEditor.Entity).Id);
        Set(editedBooking, "NumberOfParticipants", 7);
        await editedBooking.SaveCommand.ExecuteAsync();
        Check(editedBooking.Error.Length == 0 && bookings.Selected is Reservation { NumberOfParticipants: 7 }
            && Filter(bookings, "Search").Text == "Visible activity", "Reservation edit immediately updates the selected row and retains matching filters");

        var equipment = provider.GetRequiredService<EquipmentViewModel>();
        await equipment.RefreshAsync();
        Filter(equipment, "Search").Text = "does not match";
        var equipmentEditor = await equipment.CreateEditorAsync(true);
        Set(equipmentEditor, "SerialNumber", "NEW-EQUIPMENT");
        Set(equipmentEditor, "EquipmentType", EquipmentType.Mask);
        Set(equipmentEditor, "Status", EquipmentStatus.Available);
        await equipmentEditor.SaveCommand.ExecuteAsync();
        Check(equipmentEditor.Error.Length == 0 && SelectedRecord(equipment, equipmentEditor)
            && Filter(equipment, "Search").Text.Length == 0, "New equipment appears immediately despite an old search filter");

        var retiredEditor = await equipment.CreateEditorAsync(true);
        Set(retiredEditor, "SerialNumber", "RETIRED-EQUIPMENT");
        Set(retiredEditor, "EquipmentType", EquipmentType.Mask);
        Set(retiredEditor, "Status", EquipmentStatus.Retired);
        await retiredEditor.SaveCommand.ExecuteAsync();
        Check(retiredEditor.Error.Length == 0 && SelectedRecord(equipment, retiredEditor) && Filter(equipment, "ShowInactive").Checked,
            "Saving inactive equipment enables inactive visibility and selects the saved row");

        var countBeforeFailure = equipment.Rows.Count;
        var invalidEditor = await equipment.CreateEditorAsync(true);
        await invalidEditor.SaveCommand.ExecuteAsync();
        Check(invalidEditor.Error.Length > 0 && equipment.Rows.Count == countBeforeFailure,
            "Failed validation does not add a row or change the displayed list");

        var cash = provider.GetRequiredService<TransactionViewModel>();
        await cash.RefreshAsync();
        var currencies = await provider.GetRequiredService<CurrencyService>().ListAsync();
        var currency = currencies.Single(x => x.Code == "EUR");
        var cashEditor = await cash.CreateEditorAsync(new Transaction { TransactionDate = DateTime.Today.AddYears(-1) });
        Set(cashEditor, "CurrencyId", currency.Id);
        Set(cashEditor, "Amount", 25m);
        await cashEditor.SaveCommand.ExecuteAsync();
        Check(cashEditor.Error.Length == 0 && SelectedRecord(cash, cashEditor) && Filter(cash, "Year").Text.Length == 0
            && cash.Balances.Single(x => x.Code == "EUR").Balance == 25m,
            "Cash save immediately reloads both the row and balance and clears the hiding year filter");
    }
}
