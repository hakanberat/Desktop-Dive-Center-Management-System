using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class CurrencyViewModel : CatalogViewModel<Currency>
{
    public CurrencyViewModel(CurrencyService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
        : base("Currencies", service, lookups, masters, dialogs)
    {
        Subtitle = "Manage codes, names and symbols. Each currency is independent. Codes with cash history cannot be changed.";
    }
}
