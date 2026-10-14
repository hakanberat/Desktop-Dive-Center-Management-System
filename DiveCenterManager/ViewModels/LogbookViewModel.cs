using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class LogbookViewModel : ScreenViewModel
{
    private readonly LogbookService _service;
    private readonly LookupService _lookups;
    private readonly MasterDataService _masters;
    private readonly DialogService _dialogs;
    public LogbookViewModel(LogbookService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
    {
        _service = service; _lookups = lookups; _masters = masters; _dialogs = dialogs;
        Title = "Logbook"; Subtitle = "Shared dive conditions and individual diver records. Create new logs from completed Dive Operations.";
        Columns = EntitySchema.Columns(typeof(DiveLog));
        Filters.Add(new FieldViewModel("From", "Date from", typeof(DateTime?), null));
        Filters.Add(new FieldViewModel("To", "Date to", typeof(DateTime?), null));
        Filters.Add(new FieldViewModel("DiveSiteId", "Dive site", typeof(int?), null, lookup: true));
        Action("Open dive details", () => OpenDetailsAsync(((DiveLog)Selected!).Id), true);
        Action("Edit shared / personal data", async () => await EditAsync(await _service.GetAsync(((DiveLog)Selected!).Id)), true);
    }
    public override async Task LoadAsync()
    {
        Filter("DiveSiteId").Choices = [new Choice(null, "All sites"), .. await _lookups.GetAsync("DiveSiteId", includeInactive: true)];
        SetRows(await _service.ListAsync(Filter("From").Date, Filter("To").Date, Filter("DiveSiteId").SelectedValue as int?));
    }
    public DiveLogDetailViewModel CreateDetail(int id) => new(id, _service, _lookups, _masters, _dialogs,
        async () => await EditAsync(await _service.GetAsync(id)));
    public async Task OpenDetailsAsync(int id)
    {
        await _dialogs.ShowScreenAsync(CreateDetail(id));
        await RefreshAsync();
    }
    public async Task CreateFromOperationAsync(int operationId)
    {
        var log = await _service.DraftFromOperationAsync(operationId);
        if (log.Id == 0) await EditAsync(log);
        if (log.Id > 0) await OpenDetailsAsync(log.Id);
    }
    public async Task<EditorViewModel> CreateEditorAsync(DiveLog log)
    {
        var editor = new EditorViewModel(log.Id == 0 ? "Record completed dive" : $"Dive log #{log.Id}", log, entity => SaveAndRefreshAsync(entity, value => _service.SaveAsync((DiveLog)value)));
        await editor.InitializeAsync(_lookups, _masters);
        editor.Hint = "Enter shared dive measurements, then review each diver on the Diver logs tab. All changes save together.";
        editor.ReferenceLabel = "Completed dive operation";
        editor.ReferenceValue = $"Operation #{log.DiveOperationId}";
        editor.Summary = "Enter the actual exit time, depth and bottom time. Exit must be on or after entry on the dive date. Blank personal depth and time use the shared values.";
        editor.Children = new ChildCollectionViewModel("Personal diver logs", typeof(DiverDiveLog), log.DiverLogs, _lookups, _masters, _dialogs);
        return editor;
    }
    public async Task EditAsync(DiveLog log) { _dialogs.Edit(await CreateEditorAsync(log)); }
}
