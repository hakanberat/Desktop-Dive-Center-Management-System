using System.Collections.ObjectModel;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;
using DiveCenterManager.Commands;

namespace DiveCenterManager.ViewModels;

public sealed record ScheduleDay(DateTime Date, List<Reservation> Reservations, AsyncRelayCommand SelectCommand)
{
    public string Heading => Date.ToString("ddd, dd MMM");
    public string Summary => $"{Reservations.Count} bookings · {Reservations.Where(x => x.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow).Sum(x => x.NumberOfParticipants)} participants";
}

public class ScheduleViewModel : ScreenViewModel
{
    private readonly ReservationService _reservations;
    private readonly Func<ReservationViewModel> _reservationScreen;
    public bool IsCalendar { get; }
    public bool IsWeekView => DayCount == 7;
    public AsyncRelayCommand OpenReservationCommand { get; }
    public ObservableCollection<ScheduleDay> Days { get; } = [];
    public ScheduleViewModel(bool calendar, ReservationService reservations, Func<ReservationViewModel> reservationScreen)
    {
        IsCalendar = calendar; _reservations = reservations; _reservationScreen = reservationScreen;
        Title = calendar ? "Reservation Calendar" : "Daily Operations";
        Subtitle = "Plan reservations by day or week. Open a booking to edit its details, status or payments.";
        Columns = EntitySchema.Columns(typeof(Reservation));
        Filters.Add(new FieldViewModel("Date", "Selected date", typeof(DateTime), DateTime.Today, required: true));
        if (calendar) Filters.Add(new FieldViewModel("View", "View", typeof(string), "Week", lookup: true) { Choices = [new Choice("Day", "Day"), new Choice("Week", "Week")] });
        Action("‹ Previous", () => ShiftAsync(-1));
        Action("Today", async () => { Filter("Date").Date = DateTime.Today; await RefreshAsync(); });
        Action("Next ›", () => ShiftAsync(1));
        OpenReservationCommand = new AsyncRelayCommand(async () => { await _reservationScreen().OpenDetailsAsync(((Reservation)Selected!).Id); await RefreshAsync(); }, Fail, () => Selected is Reservation);
        Actions.Add(new ActionItem("Reservation details", OpenReservationCommand));
        Action("+ New reservation", async () => { await _reservationScreen().EditAsync(null, (DateTime)Filter("Date").Read()!); await RefreshAsync(); });
    }
    public ReservationDetailViewModel CreateSelectedReservationDetail() => _reservationScreen().CreateDetail(
        (Selected as Reservation ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Select a reservation first.")).Id);
    private int DayCount => IsCalendar && (Filter("View").SelectedValue as string ?? "Week") == "Week" ? 7 : 1;
    private async Task ShiftAsync(int direction)
    {
        var date = (DateTime)Filter("Date").Read()!;
        var ticks = date.Ticks + direction * DayCount * TimeSpan.TicksPerDay;
        Validation.Require(ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks, "The selected period is outside the supported calendar range.");
        Filter("Date").SetValue(new DateTime(ticks)); await RefreshAsync();
    }
    public override async Task LoadAsync()
    {
        var date = (DateTime)Filter("Date").Read()!;
        if (IsCalendar) Validation.Require(Filter("View").SelectedValue is "Day" or "Week", "Select Day or Week view.");
        OnPropertyChanged(nameof(IsWeekView));
        if (DayCount == 7) date = date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7));
        var count = Math.Min(DayCount, (DateTime.MaxValue.Date - date).Days + 1);
        var reservations = await _reservations.ListAsync(new ReservationFilter(date, date.AddDays(count - 1)));
        SetRows(reservations);
        Days.Clear();
        for (var i = 0; i < count; i++)
        {
            var selectedDay = date.AddDays(i);
            Days.Add(new ScheduleDay(selectedDay, reservations.Where(x => x.ActivityDate.Date == selectedDay).ToList(), new AsyncRelayCommand(async () =>
            {
                Filter("Date").SetValue(selectedDay);
                if (IsCalendar) Filter("View").SetValue("Day");
                await RefreshAsync();
            }, Fail)));
        }
        Cards.Clear();
        var active = reservations.Where(x => x.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow).ToList();
        Cards.Add(new MetricCard("Scheduled bookings", active.Count.ToString(), $"{date:dd MMM} – {date.AddDays(count - 1):dd MMM}"));
        Cards.Add(new MetricCard("Participants", active.Sum(x => x.NumberOfParticipants).ToString(), "Excludes cancellations and no-shows"));
    }
}
