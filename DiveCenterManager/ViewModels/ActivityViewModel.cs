using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class ActivityViewModel : CatalogViewModel<Activity>
{
    public ActivityViewModel(ActivityService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
        : base("Activities / Courses", service, lookups, masters, dialogs)
    {
        Subtitle = "Manage diving, courses and rentals. A default price uses its selected currency independently.";
        AddEnumFilter<ActivityType>("ActivityType", "Activity type");
    }

    protected override IEnumerable<Activity> FilterRecords(IEnumerable<Activity> records) =>
        Filter("ActivityType").SelectedValue is ActivityType type ? records.Where(x => x.ActivityType == type) : records;
}
