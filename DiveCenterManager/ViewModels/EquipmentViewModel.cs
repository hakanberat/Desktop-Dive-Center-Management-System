using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class EquipmentViewModel : CatalogViewModel<Equipment>
{
    public EquipmentViewModel(EquipmentService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
        : base("Equipment", service, lookups, masters, dialogs)
    {
        Subtitle = "Track equipment, serial numbers, sizes and status. Retired items are inactive; reactivation places them in Maintenance.";
        AddEnumFilter<EquipmentType>("EquipmentType", "Equipment type");
        AddEnumFilter<EquipmentStatus>("Status", "Status");
    }

    protected override IEnumerable<Equipment> FilterRecords(IEnumerable<Equipment> records)
    {
        if (Filter("EquipmentType").SelectedValue is EquipmentType type) records = records.Where(x => x.EquipmentType == type);
        if (Filter("Status").SelectedValue is EquipmentStatus status) records = records.Where(x => x.Status == status);
        return records;
    }
}
