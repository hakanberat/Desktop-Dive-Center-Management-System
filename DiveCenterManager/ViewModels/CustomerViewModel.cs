using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class CustomerViewModel : CatalogViewModel<Customer>
{
    public CustomerViewModel(CustomerService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
        : base("Customers / Divers", service, lookups, masters, dialogs)
    {
        Subtitle = "Customer profiles, prior dive totals and emergency contacts. Certification is optional for first-time divers.";
        AddEnumFilter<CertificationAgency>("Agency", "Certification agency");
        Action("Reservation & dive history", () => dialogs.ShowScreenAsync(new CustomerHistoryViewModel((Customer)Selected!, service)), true);
    }

    protected override IEnumerable<Customer> FilterRecords(IEnumerable<Customer> records) =>
        Filter("Agency").SelectedValue is CertificationAgency agency ? records.Where(x => x.CertificationAgency == agency) : records;
}
