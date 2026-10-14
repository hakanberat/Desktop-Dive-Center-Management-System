using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class MasterDataViewModel : ScreenViewModel
{
    private readonly Type _type;
    private readonly MasterDataService _service;
    private readonly LookupService _lookups;
    private readonly DialogService _dialogs;
    public MasterDataViewModel(Type type, string title, MasterDataService service, LookupService lookups, DialogService dialogs)
    {
        _type = type; _service = service; _lookups = lookups; _dialogs = dialogs;
        Title = title;
        Subtitle = type == typeof(Customer) ? "Customer profiles and diver records. Certification is optional for first-time divers." : "Manage the records your team uses every day. Deactivation preserves historical records.";
        Columns = EntitySchema.Columns(type);
        Filters.Add(new FieldViewModel("Search", "Search", typeof(string), null));
        Filters.Add(new FieldViewModel("ShowInactive", "Include inactive", typeof(bool), false));
        Action("+ Add", () => EditAsync(true));
        Action("Edit", () => EditAsync(false), true);
        Action("Activate / deactivate", async () => { await _service.ToggleActiveAsync(Selected!); await RefreshAsync(); }, true);
    }
    public void AddHistoryAction(Func<Customer, Task> history) { if (_type == typeof(Customer)) Action("Reservation & dive history", () => history((Customer)Selected!), true); }
    public override async Task LoadAsync()
    {
        var records = await _service.ListAsync(_type);
        var search = SearchText;
        SetRows(records.Where(x => (Filter("ShowInactive").Checked || ((IActiveEntity)x).IsActive) && (search.Length == 0 || x.GetType().GetProperties().Where(p => EntitySchema.IsScalar(p.PropertyType)).Any(p => Convert.ToString(p.GetValue(x))?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true))));
    }
    private async Task EditAsync(bool create)
    {
        var entity = create ? Activator.CreateInstance(_type)! : EntityCopy.Copy(Selected!);
        var editor = new EditorViewModel((create ? "Add " : "Edit ") + EntitySchema.Words(_type.Name), entity, changed => SaveAndRefreshAsync(changed, _service.SaveAsync));
        await editor.InitializeAsync(_lookups, _service);
        _dialogs.Edit(editor);
    }
}
