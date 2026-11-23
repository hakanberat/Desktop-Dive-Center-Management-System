using System.Collections.ObjectModel;
using DiveCenterManager.Models;
using DiveCenterManager.Services;
using DiveCenterManager.Data;
using DiveCenterManager.Helpers;
using DiveCenterManager.Commands;
using Microsoft.Win32;
using System.IO;

namespace DiveCenterManager.ViewModels;

public record NavigationItem(string Title, Func<ScreenViewModel> Create);

public sealed class MainViewModel : ViewModelBase
{
    public ObservableCollection<NavigationItem> Navigation { get; } = [];
    private NavigationItem? _selectedNavigation;
    public NavigationItem? SelectedNavigation { get => _selectedNavigation; set { if (Set(ref _selectedNavigation, value) && value != null) NavigationTask = OpenAsync(value); } }
    public Task NavigationTask { get; private set; } = Task.CompletedTask;
    private ScreenViewModel? _current;
    public ScreenViewModel? Current { get => _current; private set => Set(ref _current, value); }
    public AsyncRelayCommand BackupCommand { get; }
    public RelayCommand OpenBackupFolderCommand { get; }
    private readonly BackupService _backups;
    private string _notice = "";
    public string Notice { get => _notice; private set { Set(ref _notice, value); OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => Notice.Length > 0;
    private bool _noticeIsError;
    public bool NoticeIsError { get => _noticeIsError; private set => Set(ref _noticeIsError, value); }
    public RelayCommand DismissNoticeCommand { get; }
    public void ShowNotice(string text, bool error = true) { NoticeIsError = error; Notice = text; }
    public async Task BackupToAsync(string destination)
    {
        ShowNotice("Creating and verifying the database backup...", false);
        await _backups.CreateAsync(destination);
        ShowNotice("Verified database backup saved to: " + Path.GetFullPath(destination), false);
    }
    public MainViewModel(MasterDataService masters, LookupService lookups, DialogService dialogs, ReservationService reservations, TransactionService transactions, DashboardService dashboard, ReportService reports, BackupService backups,
        ActivityViewModel activityScreen, DiveSiteViewModel siteScreen,
        StaffViewModel staffScreen, BoatViewModel boatScreen, EquipmentViewModel equipmentScreen, SettingsViewModel settingsScreen)
    {
        _backups = backups;
        DismissNoticeCommand = new RelayCommand(() => Notice = "");
        BackupCommand = new AsyncRelayCommand(async () =>
        {
            var picker = new SaveFileDialog { Title = "Backup Dive Hub database", Filter = "SQLite database (*.db)|*.db", FileName = $"DiveCenter-{DateTime.Now:yyyyMMdd-HHmmss}.db", InitialDirectory = backups.Folder, OverwritePrompt = true, AddExtension = true };
            if (picker.ShowDialog() == true) await BackupToAsync(picker.FileName);
        }, e => ShowNotice("Backup failed. " + ErrorMessages.Friendly(e)));
        OpenBackupFolderCommand = new RelayCommand(() => { try { backups.OpenFolder(); } catch (Exception e) { ShowNotice(ErrorMessages.Friendly(e)); } });
        TransactionViewModel TransactionScreen() => new(transactions, lookups, masters, dialogs);
        ReservationViewModel ReservationScreen() { var vm = new ReservationViewModel(reservations, lookups, masters, dialogs); vm.AddPaymentAction(r => TransactionScreen().AddPaymentAsync(r)); return vm; }
        Navigation.Add(new NavigationItem("Dashboard", () => new DashboardViewModel(dashboard)));
        Navigation.Add(new NavigationItem("Reservations", ReservationScreen));
        Navigation.Add(new NavigationItem("Calendar", () => new CalendarViewModel(reservations, ReservationScreen)));
        Navigation.Add(new NavigationItem("Activities / Courses", () => activityScreen));
        Navigation.Add(new NavigationItem("Dive Sites", () => siteScreen));
        Navigation.Add(new NavigationItem("Staff", () => staffScreen));
        Navigation.Add(new NavigationItem("Boats", () => boatScreen));
        Navigation.Add(new NavigationItem("Equipment", () => equipmentScreen));
        Navigation.Add(new NavigationItem("Transactions", TransactionScreen));
        Navigation.Add(new NavigationItem("Reports", () => new ReportsViewModel(reports, lookups)));
        Navigation.Add(new NavigationItem("Settings", () => settingsScreen));
    }
    public async Task OpenAsync(NavigationItem item)
    {
        _selectedNavigation = item;
        OnPropertyChanged(nameof(SelectedNavigation));
        try
        {
            var screen = item.Create();
            Current = screen;
            try { await screen.RefreshAsync(); }
            catch (Exception exception) { screen.Message = ErrorMessages.Friendly(exception); }
        }
        catch (Exception exception) { ShowNotice(ErrorMessages.Friendly(exception)); }
    }
}
