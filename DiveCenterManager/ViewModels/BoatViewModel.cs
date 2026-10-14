using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class BoatViewModel : CatalogViewModel<Boat>
{
    public BoatViewModel(BoatService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
        : base("Boats", service, lookups, masters, dialogs)
    {
        Subtitle = "Manage boat names, registration numbers, capacity and notes. Deactivation preserves past assignments.";
        Filters.Add(new FieldViewModel("Capacity", "Minimum capacity", typeof(int?), null));
    }

    protected override IEnumerable<Boat> FilterRecords(IEnumerable<Boat> records)
    {
        var minimum = (int?)Filter("Capacity").Read();
        Validation.Require(minimum is null or >= 0, "Minimum capacity cannot be negative.");
        return minimum.HasValue ? records.Where(x => x.Capacity >= minimum.Value) : records;
    }
}
