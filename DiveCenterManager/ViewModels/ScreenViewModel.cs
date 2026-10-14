using System.Collections.ObjectModel;
using System.Windows.Input;
using DiveCenterManager.Commands;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;

namespace DiveCenterManager.ViewModels;

public record ActionItem(string Label, ICommand Command);
public record MetricCard(string Label, string Value, string Detail = "");

public abstract class ScreenViewModel : ViewModelBase
{
    public string Title { get; protected set; } = "";
    public string Subtitle { get; protected set; } = "";
    private List<ColumnSpec> _columns = [];
    public List<ColumnSpec> Columns { get => _columns; protected set => Set(ref _columns, value); }
    public ObservableCollection<object> Rows { get; } = [];
    public ObservableCollection<ActionItem> Actions { get; } = [];
    public ObservableCollection<FieldViewModel> Filters { get; } = [];
    public ObservableCollection<MetricCard> Cards { get; } = [];
    public bool HasFilters => Filters.Count > 0;
    private object? _selected;
    public object? Selected { get => _selected; set { Set(ref _selected, value); foreach (var action in Actions) { if (action.Command is AsyncRelayCommand c) c.RaiseCanExecuteChanged(); if (action.Command is RelayCommand r) r.RaiseCanExecuteChanged(); } OnSelectionChanged(); } }
    private string _message = "";
    public string Message { get => _message; set => Set(ref _message, value); }
    private bool _busy;
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }
    public string ResultCount => $"{Rows.Count:N0} records";
    public bool IsEmpty => !IsBusy && Rows.Count == 0;
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ClearFiltersCommand { get; }
    protected ScreenViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, Fail);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync, Fail);
    }
    protected virtual async Task ClearFiltersAsync() { foreach (var field in Filters.ToArray()) field.SetValue(field.Kind == "Boolean" ? false : null); await RefreshAsync(); }
    public async Task RefreshAsync()
    {
        IsBusy = true; Message = "";
        try { foreach (var field in Filters.Where(x => x.Kind == "Date")) field.Read(); await LoadAsync(); OnPropertyChanged(nameof(ResultCount)); }
        finally { IsBusy = false; OnPropertyChanged(nameof(IsEmpty)); }
    }
    public abstract Task LoadAsync();
    protected async Task SaveAndRefreshAsync(object record, Func<object, Task> save)
    {
        await save(record);
        // Saving has committed. A reload failure must not leave the editor offering
        // to insert the same record again.
        try
        {
            var id = ((IEntity)record).Id;
            object? SavedRow() => Rows.FirstOrDefault(row => row.GetType() == record.GetType()
                && row is IEntity entity && entity.Id == id);
            await RefreshAsync();
            var selected = SavedRow();
            if (selected == null && HasFilters)
            {
                await ClearFiltersAsync();
                // Inactive catalog records are excluded even with cleared filters.
                var inactive = Filters.FirstOrDefault(field => field.Key == "ShowInactive");
                if (SavedRow() == null && inactive != null)
                {
                    inactive.SetValue(true);
                    await RefreshAsync();
                }
                selected = SavedRow();
                Message = "Record saved. Filters were cleared to show the saved record.";
            }
            Selected = selected;
        }
        catch (Exception exception)
        {
            Message = "Record saved, but the list could not be refreshed. " + ErrorMessages.Friendly(exception);
        }
    }
    protected virtual void OnSelectionChanged() { }
    protected void SetRows(IEnumerable<object> rows) { Selected = null; Rows.Clear(); foreach (var row in rows) Rows.Add(row); OnPropertyChanged(nameof(ResultCount)); }
    protected void Fail(Exception e) => Message = ErrorMessages.Friendly(e);
    protected void Action(string label, Func<Task> execute, bool selection = false) => Actions.Add(new ActionItem(label, new AsyncRelayCommand(execute, Fail, () => !selection || Selected != null)));
    protected FieldViewModel Filter(string key) => Filters.First(x => x.Key == key);
    protected string SearchText => Filters.FirstOrDefault(x => x.Key == "Search")?.Text.Trim() ?? "";
}
