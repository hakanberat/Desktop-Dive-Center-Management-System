using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class TransactionCategoryViewModel : CatalogViewModel<TransactionCategory>
{
    public TransactionCategoryViewModel(TransactionCategoryService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
        : base("Transaction Categories", service, lookups, masters, dialogs)
    {
        Subtitle = "Manage Income, Expense or Both categories. Existing transactions are preserved when a category is deactivated.";
        AddEnumFilter<CategoryType>("CategoryType", "Category type");
    }

    protected override IEnumerable<TransactionCategory> FilterRecords(IEnumerable<TransactionCategory> records) =>
        Filter("CategoryType").SelectedValue is CategoryType type ? records.Where(x => x.Type == type) : records;
}
