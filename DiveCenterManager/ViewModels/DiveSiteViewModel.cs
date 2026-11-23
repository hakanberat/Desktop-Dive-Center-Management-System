using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class DiveSiteViewModel : CatalogViewModel<DiveSite>
{
    public DiveSiteViewModel(DiveSiteService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
        : base("Dive Sites", service, lookups, masters, dialogs)
    {
        Subtitle = "Maintain locations, coordinates, depths and site notes. Inactive sites remain in historical records.";
        Columns.Insert(2, new("Latitude", "Latitude"));
        Columns.Insert(3, new("Longitude", "Longitude"));
    }
}
