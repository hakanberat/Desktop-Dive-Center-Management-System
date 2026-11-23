using System.Collections.ObjectModel;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public record ReservationDetailField(string Label, string Value);

public sealed class ReservationDetailViewModel : ScreenViewModel
{
    private readonly int _id;
    private readonly ReservationService _service;
    public ObservableCollection<ReservationDetailField> Details { get; } = [];
    public ObservableCollection<Transaction> Payments { get; } = [];
    public List<ColumnSpec> PaymentColumns { get; } = EntitySchema.Columns(typeof(Transaction));
    public string PaymentCount => $"{Payments.Count} linked cash transaction(s)";
    private string _paymentSummary = "";
    public string PaymentSummary { get => _paymentSummary; private set => Set(ref _paymentSummary, value); }

    public ReservationDetailViewModel(int id, ReservationService service, DialogService dialogs, Func<int?, Task> edit, Func<Reservation, Task>? payment = null)
    {
        _id = id; _service = service;
        Title = "Reservation details";
        Subtitle = "Booking information, participant count and payments.";
        Action("Edit reservation", async () => { await edit(id); await RefreshAsync(); });
        Action("Cancel reservation", async () =>
        {
            if (dialogs.Confirm("Cancel this reservation? Its participants and cash history will be retained."))
            { await service.CancelAsync(id); await RefreshAsync(); }
        });
        if (payment != null) Action("Add reservation payment", async () => { await payment(await service.GetAsync(id)); await RefreshAsync(); });
    }

    public override async Task LoadAsync()
    {
        var reservation = await _service.GetAsync(_id);
        Title = reservation.ReservationNumber;
        OnPropertyChanged(nameof(Title));
        Details.Clear();
        Add("Reservation number", reservation.ReservationNumber);
        Add("Reservation date", reservation.ReservationDate.ToString("dd MMM yyyy"));
        Add("Activity date", reservation.ActivityDate.ToString("dd MMM yyyy"));
        Add("Start time", reservation.StartTime.ToString(@"hh\:mm"));
        Add("Activity", reservation.Activity?.Name);
        Add("Status", EntitySchema.Words(reservation.Status.ToString()));
        Add("Instructor", reservation.Instructor?.FullName);
        Add("Dive site", reservation.DiveSite?.Name);
        Add("Boat", reservation.Boat?.Name);
        Add("Participants", reservation.NumberOfParticipants.ToString());
        Add("Notes", reservation.Notes);
        PaymentSummary = await _service.PaymentSummaryAsync(_id);
        Payments.Clear();
        foreach (var payment in await _service.PaymentsAsync(_id)) Payments.Add(payment);
        OnPropertyChanged(nameof(PaymentCount));

        void Add(string label, string? value) => Details.Add(new(label, string.IsNullOrWhiteSpace(value) ? "—" : value));
    }
}
