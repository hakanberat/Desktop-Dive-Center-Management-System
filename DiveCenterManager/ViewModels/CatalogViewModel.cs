using DiveCenterManager.Commands;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

// Reuses list/editor presentation while each module supplies its typed service and filters.
public abstract class CatalogViewModel<T> : ScreenViewModel where T : class, IActiveEntity, new()
{
    private readonly ICatalogService<T> _service;
    private readonly LookupService _lookups;
    private readonly MasterDataService _masters;
    private readonly DialogService _dialogs;

    protected CatalogViewModel(string title, ICatalogService<T> service, LookupService lookups, MasterDataService masters, DialogService dialogs)
    {
        Title = title;
        _service = service; _lookups = lookups; _masters = masters; _dialogs = dialogs;
        Columns = EntitySchema.Columns(typeof(T));
        Filters.Add(new FieldViewModel("Search", "Search", typeof(string), null));
        Filters.Add(new FieldViewModel("ShowInactive", "Include inactive", typeof(bool), false));
        Action("+ Add", () => EditAsync(true));
        Action("Edit", () => EditAsync(false), true);
        Actions.Add(new ActionItem("Deactivate", new AsyncRelayCommand(() => SetActiveAsync(false), Fail, () => Selected is T { IsActive: true })));
        Actions.Add(new ActionItem("Activate", new AsyncRelayCommand(() => SetActiveAsync(true), Fail, () => Selected is T { IsActive: false })));
    }

    public override async Task LoadAsync()
    {
        await PrepareFiltersAsync();
        var records = await _service.ListAsync();
        var search = SearchText;
        var visible = records.Where(x => Filter("ShowInactive").Checked || x.IsActive);
        if (search.Length > 0)
            visible = visible.Where(x => x.ToString()?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true ||
                typeof(T).GetProperties().Where(p => EntitySchema.IsScalar(p.PropertyType))
                    .Any(p => Convert.ToString(p.GetValue(x))?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true));
        SetRows(FilterRecords(visible));
    }

    protected virtual Task PrepareFiltersAsync() => Task.CompletedTask;
    protected virtual IEnumerable<T> FilterRecords(IEnumerable<T> records) => records;

    protected void AddEnumFilter<TEnum>(string key, string label) where TEnum : struct, Enum
    {
        var field = new FieldViewModel(key, label, typeof(TEnum?), null);
        field.Choices[0] = new Choice(null, "All");
        Filters.Add(field);
    }

    public async Task<EditorViewModel> CreateEditorAsync(bool create)
    {
        var entity = create ? new T() : (T)EntityCopy.Copy(Selected as T ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Select a record to edit."));
        var editor = new EditorViewModel((create ? "Add " : "Edit ") + EntitySchema.Words(typeof(T).Name), entity, changed => SaveAndRefreshAsync(changed, value => _service.SaveAsync((T)value)));
        await editor.InitializeAsync(_lookups, _masters);
        if (entity is Equipment) editor.Hint += " Retired equipment is inactive. Reactivation places it in Maintenance.";
        return editor;
    }

    private async Task EditAsync(bool create)
    {
        _dialogs.Edit(await CreateEditorAsync(create));
    }

    private async Task SetActiveAsync(bool active)
    {
        var selected = Selected as T ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Select a record first.");
        await _service.SetActiveAsync(selected.Id, active);
        await RefreshAsync();
    }
}
