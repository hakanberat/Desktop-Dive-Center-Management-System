using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class CustomerHistoryViewModel : ScreenViewModel
{
    private readonly CustomerService _service;
    private readonly int _customerId;
    public CustomerHistoryViewModel(Customer customer, CustomerService service)
    {
        _customerId = customer.Id; _service = service; Title = customer.FullName;
        Subtitle = "Reservation and dive history. Personal depth and time overrides are shown when entered.";
        Filters.Add(new FieldViewModel("History", "History", typeof(string), "Dives", lookup: true) { Choices = [new Choice("Dives", "Dive history"), new Choice("Reservations", "Reservation history")] });
    }
    public override async Task LoadAsync()
    {
        var history = await _service.HistoryAsync(_customerId);
        Cards.Clear();
        Cards.Add(new MetricCard("Total logged dives", history.Customer.TotalLoggedDives.ToString(), "Includes prior dives entered on the profile"));
        Cards.Add(new MetricCard("Dives at this center", history.Dives.Count.ToString()));
        Cards.Add(new MetricCard("Reservations", history.Reservations.Count.ToString()));
        if (Filter("History").SelectedValue as string == "Reservations") { Columns = EntitySchema.Columns(typeof(Reservation)); SetRows(history.Reservations); }
        else
        {
            Columns = [new("Date", "Date", "{0:dd MMM yyyy}"), new("Site", "Site"), new("Instructor", "Instructor"), new("Buddy", "Buddy"), new("MaxDepthMeters", "Max depth (m)"), new("BottomTimeMinutes", "Bottom time (min)"), new("GasType", "Gas"), new("Verified", "Verified"), new("Notes", "Personal notes")];
            SetRows(history.Dives);
        }
    }
}
