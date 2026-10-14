using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class StaffViewModel : CatalogViewModel<Staff>
{
    private readonly StaffRoleService _roles;
    public StaffViewModel(StaffService service, StaffRoleService roles, LookupService lookups, MasterDataService masters, DialogService dialogs)
        : base("Staff", service, lookups, masters, dialogs)
    {
        _roles = roles;
        Subtitle = "Manage staff contacts, instructor credentials and multiple roles. Existing inactive roles can be retained.";
        Filters.Add(new FieldViewModel("StaffRole", "Role", typeof(int?), null, lookup: true));
        Action("Manage roles", () => dialogs.ShowScreenAsync(new MasterDataViewModel(typeof(StaffRole), "Staff roles", masters, lookups, dialogs)));
    }

    protected override async Task PrepareFiltersAsync()
    {
        Filter("StaffRole").Choices = [new Choice(null, "All roles"), .. (await _roles.ListAsync()).Select(x => new Choice(x.Id, x.Name + (x.IsActive ? "" : " (inactive)")))];
    }

    protected override IEnumerable<Staff> FilterRecords(IEnumerable<Staff> records) =>
        Filter("StaffRole").SelectedValue is int id ? records.Where(x => x.StaffRoles.Any(r => r.StaffRoleId == id)) : records;
}
