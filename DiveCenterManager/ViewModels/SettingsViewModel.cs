using DiveCenterManager.Data;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public record SettingRow(string Setting, string Value);
public sealed class SettingsViewModel : ScreenViewModel
{
    private readonly AppPaths _paths;
    private readonly BackupService _backups;
    public SettingsViewModel(AppPaths paths, BackupService backups, MasterDataService masters, LookupService lookups, DialogService dialogs, ReferenceDefaults defaults,
        ActivityViewModel activities, CurrencyViewModel currencies, TransactionCategoryViewModel categories, DiveSiteViewModel sites)
    {
        _paths = paths; _backups = backups; Title = "Settings";
        Subtitle = "Manage currencies, categories, roles, and editable catalog defaults for your center.";
        Columns = [new("Setting", "Setting"), new("Value", "Value")];
        foreach (var screen in new ScreenViewModel[] { activities, currencies, categories, sites })
            Action(screen.Title, () => dialogs.ShowScreenAsync(screen));
        Action("Staff roles", () => dialogs.ShowScreenAsync(new MasterDataViewModel(typeof(StaffRole), "Staff roles", masters, lookups, dialogs)));
        Action("Add activity / site defaults", async () =>
        {
            if (dialogs.Confirm("Add the optional activity names and public Dive Hub dive-site names? Existing records will be preserved. No customers, reservations, or transactions will be added."))
            { await defaults.AddAsync(); await RefreshAsync(); }
        });
    }
    public override Task LoadAsync()
    {
        SetRows(new[]
        {
            new SettingRow("Database", _paths.DatabasePath), new SettingRow("Backup folder", _backups.Folder),
            new SettingRow("Storage", "Local SQLite · works completely offline"), new SettingRow("Cash balances", "Independent currencies · no conversion"),
            new SettingRow("Catalog defaults", "Optional · use Add activity / site defaults above"),
            new SettingRow("Sample business data", _paths.IsSampleWorkspace ? "Enabled in this separate sample workspace." : "Disabled. Use the separate sample workspace to try sample records."),
            new SettingRow("Application", "Dive Hub Management · .NET 10 / WPF")
        });
        return Task.CompletedTask;
    }
}
