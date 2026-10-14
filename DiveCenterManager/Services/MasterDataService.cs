using DiveCenterManager.Models;

namespace DiveCenterManager.Services;

// Compatibility entry point for existing lookups, editors and sample-data callers.
// Each catalog's typed service owns its EF queries and business rules.
public sealed class MasterDataService(
    CustomerService customers, ActivityService activities, DiveSiteService sites,
    StaffService staff, BoatService boats, EquipmentService equipment,
    CurrencyService currencies, TransactionCategoryService categories, StaffRoleService roles)
{
    public static readonly Type[] SupportedTypes = [typeof(Customer), typeof(Activity), typeof(DiveSite), typeof(Staff), typeof(Boat), typeof(Equipment), typeof(Currency), typeof(TransactionCategory), typeof(StaffRole)];

    public async Task<List<object>> ListAsync(Type type)
    {
        if (type == typeof(Customer)) return (await customers.ListAsync()).Cast<object>().ToList();
        if (type == typeof(Activity)) return (await activities.ListAsync()).Cast<object>().ToList();
        if (type == typeof(DiveSite)) return (await sites.ListAsync()).Cast<object>().ToList();
        if (type == typeof(Staff)) return (await staff.ListAsync()).Cast<object>().ToList();
        if (type == typeof(Boat)) return (await boats.ListAsync()).Cast<object>().ToList();
        if (type == typeof(Equipment)) return (await equipment.ListAsync()).Cast<object>().ToList();
        if (type == typeof(Currency)) return (await currencies.ListAsync()).Cast<object>().ToList();
        if (type == typeof(TransactionCategory)) return (await categories.ListAsync()).Cast<object>().ToList();
        if (type == typeof(StaffRole)) return (await roles.ListAsync()).Cast<object>().ToList();
        throw new ArgumentException("Unsupported master-data catalog.", nameof(type));
    }

    public Task SaveAsync(object record) => record switch
    {
        Customer x => customers.SaveAsync(x), Activity x => activities.SaveAsync(x),
        DiveSite x => sites.SaveAsync(x), Staff x => staff.SaveAsync(x),
        Boat x => boats.SaveAsync(x), Equipment x => equipment.SaveAsync(x),
        Currency x => currencies.SaveAsync(x), TransactionCategory x => categories.SaveAsync(x),
        StaffRole x => roles.SaveAsync(x),
        _ => throw new ArgumentException("This record cannot be saved as master data.", nameof(record))
    };

    public async Task ToggleActiveAsync(object record)
    {
        Validation.Require(record is IActiveEntity && SupportedTypes.Contains(record.GetType()), "This record cannot be deactivated.");
        var id = ((IActiveEntity)record).Id;
        var saved = (await ListAsync(record.GetType())).Cast<IActiveEntity>().SingleOrDefault(x => x.Id == id)
            ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Record no longer exists. Refresh the list.");
        await SetActiveAsync(saved, !saved.IsActive);
    }

    public Task SetActiveAsync(IActiveEntity record, bool active) => record switch
    {
        Customer x => customers.SetActiveAsync(x.Id, active), Activity x => activities.SetActiveAsync(x.Id, active),
        DiveSite x => sites.SetActiveAsync(x.Id, active), Staff x => staff.SetActiveAsync(x.Id, active),
        Boat x => boats.SetActiveAsync(x.Id, active), Equipment x => equipment.SetActiveAsync(x.Id, active),
        Currency x => currencies.SetActiveAsync(x.Id, active), TransactionCategory x => categories.SetActiveAsync(x.Id, active),
        StaffRole x => roles.SetActiveAsync(x.Id, active),
        _ => throw new ArgumentException("Unsupported master-data catalog.", nameof(record))
    };
}
