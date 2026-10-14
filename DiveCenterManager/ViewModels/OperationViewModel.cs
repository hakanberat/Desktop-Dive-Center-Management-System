using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;
using DiveCenterManager.Commands;

namespace DiveCenterManager.ViewModels;

public sealed class OperationViewModel : ScreenViewModel
{
    private readonly DiveOperationService _service;
    private readonly LookupService _lookups;
    private readonly MasterDataService _masters;
    private readonly DialogService _dialogs;
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand CompleteCommand { get; }
    public AsyncRelayCommand CancelOperationCommand { get; }
    public OperationViewModel(DiveOperationService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
    {
        _service = service; _lookups = lookups; _masters = masters; _dialogs = dialogs;
        Title = "Dive Operations"; Subtitle = "Prepare participants, start the dive, and record completion before creating a logbook entry.";
        Columns = EntitySchema.Columns(typeof(DiveOperation));
        Filters.Add(new FieldViewModel("From", "Date from", typeof(DateTime?), DateTime.Today));
        Filters.Add(new FieldViewModel("To", "Date to", typeof(DateTime?), null));
        Filters.Add(new FieldViewModel("Status", "Status", typeof(OperationStatus?), null));
        Filters.Add(new FieldViewModel("Search", "Site / team / booking / people", typeof(string), null));
        Filter("Status").Choices[0] = new Choice(null, "All statuses");
        Action("+ Manual dive operation", () => EditAsync(new DiveOperation()));
        Action("Open / edit", async () => await EditAsync(await _service.GetAsync(((DiveOperation)Selected!).Id)), true);
        StartCommand = new AsyncRelayCommand(() => SetStatusAsync(OperationStatus.InProgress), Fail, () => Selected is DiveOperation { Status: OperationStatus.Planned });
        CompleteCommand = new AsyncRelayCommand(() => SetStatusAsync(OperationStatus.Completed), Fail, () => Selected is DiveOperation { Status: OperationStatus.InProgress });
        CancelOperationCommand = new AsyncRelayCommand(async () => { if (_dialogs.Confirm("Cancel this dive operation? The record will be retained.")) await SetStatusAsync(OperationStatus.Cancelled); }, Fail,
            () => Selected is DiveOperation { Status: OperationStatus.Planned or OperationStatus.InProgress });
        Actions.Add(new ActionItem("Start dive", StartCommand));
        Actions.Add(new ActionItem("Complete dive", CompleteCommand));
        Actions.Add(new ActionItem("Cancel operation", CancelOperationCommand));
    }
    public void AddLogAction(Func<int, Task> createLog) => Actions.Add(new ActionItem("Create dive log",
        new AsyncRelayCommand(() => createLog(((DiveOperation)Selected!).Id), Fail, () => Selected is DiveOperation { Status: OperationStatus.Completed })));
    public override async Task LoadAsync() => SetRows(await _service.ListAsync(Filter("From").Date, Filter("To").Date, Filter("Status").SelectedValue as OperationStatus?, SearchText));
    private async Task SetStatusAsync(OperationStatus next)
    {
        var id = ((DiveOperation)Selected!).Id;
        await _service.ChangeStatusAsync(id, next); await RefreshAsync();
        Selected = Rows.Cast<DiveOperation>().FirstOrDefault(x => x.Id == id);
    }
    public async Task<EditorViewModel> CreateEditorAsync(DiveOperation operation)
    {
        var editor = new EditorViewModel(operation.Id == 0 ? "Plan dive operation" : $"Dive operation #{operation.Id}", operation, entity => SaveAndRefreshAsync(entity, value => _service.SaveAsync((DiveOperation)value)));
        await editor.InitializeAsync(_lookups, _masters);
        var diveTypes = editor.Fields.Single(x => x.Key == "DiveType");
        diveTypes.Choices = diveTypes.Choices.Where(x => x.Value is ActivityType type && type is not ActivityType.EquipmentRental and not ActivityType.Other).ToList();
        editor.ReferenceLabel = "Source reservation";
        editor.ReferenceValue = operation.ReservationId.HasValue ? operation.Reservation?.ReservationNumber ?? "Linked reservation" : "Manual operation";
        editor.Summary = $"Status: {EntitySchema.Words(operation.Status.ToString())}. Planned → In progress → Completed.";
        editor.Hint = "Review the date, site, team and Participants tab before saving. Participant changes are saved together.";
        editor.Children = new ChildCollectionViewModel("Operation participants", typeof(DiveOperationParticipant), operation.Participants, _lookups, _masters, _dialogs);
        if (operation.Status is OperationStatus.Completed or OperationStatus.Cancelled)
        {
            editor.CanEdit = false; editor.Children.CanEdit = false;
            editor.Hint = "This operation is closed and read-only. Completed dive measurements can be edited in Logbook.";
        }
        return editor;
    }
    public async Task EditAsync(DiveOperation operation)
    {
        var editor = await CreateEditorAsync(operation);
        _dialogs.Edit(editor);
    }
}
