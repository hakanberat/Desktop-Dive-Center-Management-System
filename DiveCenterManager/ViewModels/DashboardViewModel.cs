using DiveCenterManager.Helpers;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class DashboardViewModel : ScreenViewModel
{
    private readonly DashboardService _service;
    private DashboardSnapshot? _snapshot;
    public DashboardSnapshot? Snapshot { get => _snapshot; private set => Set(ref _snapshot, value); }
    public string Today => DateTime.Today.ToString("dddd, dd MMMM yyyy");
    public bool HasUpcoming => Snapshot?.Upcoming.Count > 0;
    public bool HasRecentReservations => Snapshot?.RecentReservations.Count > 0;
    public List<ColumnSpec> UpcomingColumns { get; } = [new("ReservationNumber", "Reservation"), new("Date", "Date", "{0:dd MMM}"), new("StartTime", "Time", "{0:hh\\:mm}"), new("Activity", "Activity"), new("Participants", "People"), new("Status", "Status")];
    public List<ColumnSpec> RecentColumns => UpcomingColumns;
    public DashboardViewModel(DashboardService service)
    {
        _service = service; Title = "Dashboard"; Subtitle = "Reservations, participants and cash movement at a glance.";
    }
    public override async Task LoadAsync()
    {
        Snapshot = await _service.GetAsync(DateTime.Today);
        Cards.Clear();
        Cards.Add(new MetricCard("Today's reservations", Snapshot.TodayReservations.ToString(), "Active and completed bookings"));
        Cards.Add(new MetricCard("Today's upcoming bookings", Snapshot.PendingReservations.ToString(), "Pending and confirmed reservations"));
        Cards.Add(new MetricCard("Today's participants", Snapshot.Participants.ToString(), "Total people across today's bookings"));
        Cards.Add(new MetricCard("Completed bookings", Snapshot.CompletedReservations.ToString(), "Today's completed reservations"));
        OnPropertyChanged(nameof(Today)); OnPropertyChanged(nameof(HasUpcoming)); OnPropertyChanged(nameof(HasRecentReservations));
    }
}
