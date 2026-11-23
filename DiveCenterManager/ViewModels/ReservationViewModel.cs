using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class ReservationViewModel : ScreenViewModel
{
    private readonly ReservationService _service;
    private readonly LookupService _lookups;
    private readonly MasterDataService _masters;
    private readonly DialogService _dialogs;
    private Func<Reservation, Task>? _payment;
    public ReservationViewModel(ReservationService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
    {
        _service = service; _lookups = lookups; _masters = masters; _dialogs = dialogs;
        Title = "Reservations"; Subtitle = "Plan activities, choose the number of participants, and track booking status and payments.";
        Columns = EntitySchema.Columns(typeof(Reservation));
        Filters.Add(new FieldViewModel("Search", "Search bookings", typeof(string), null));
        Filters.Add(new FieldViewModel("From", "Activity date from", typeof(DateTime?), DateTime.Today));
        Filters.Add(new FieldViewModel("To", "Activity date to", typeof(DateTime?), null));
        Filters.Add(new FieldViewModel("ActivityId", "Activity", typeof(int?), null, lookup: true));
        Filters.Add(new FieldViewModel("Status", "Status", typeof(ReservationStatus?), null));
        Filter("Status").Choices[0] = new Choice(null, "All statuses");
        Action("+ New reservation", () => EditAsync(null));
        Action("Details", () => OpenDetailsAsync(((Reservation)Selected!).Id), true);
        Action("Open / edit", () => EditAsync(((Reservation)Selected!).Id), true);
        Action("Cancel reservation", async () =>
        {
            if (_dialogs.Confirm("Cancel this reservation? Its participants and cash history will be retained.")) { await _service.CancelAsync(((Reservation)Selected!).Id); await RefreshAsync(); }
        }, true);
    }
    public override async Task LoadAsync()
    {
        Filter("ActivityId").Choices = [new Choice(null, "All activities"), .. await _lookups.GetAsync("ActivityId", includeInactive: true)];
        SetRows(await _service.ListAsync(new ReservationFilter(Filter("From").Date, Filter("To").Date, Filter("ActivityId").SelectedValue as int?, Filter("Status").SelectedValue as ReservationStatus?, SearchText)));
    }
    public void AddPaymentAction(Func<Reservation, Task> payment)
    {
        _payment = payment;
        Action("Add reservation payment", async () => { await payment((Reservation)Selected!); await RefreshAsync(); }, true);
    }
    public async Task<EditorViewModel> CreateEditorAsync(int? id, DateTime? activityDate = null)
    {
        var reservation = id.HasValue ? await _service.GetAsync(id.Value) : new Reservation { ActivityDate = activityDate?.Date ?? DateTime.Today };
        var editor = new EditorViewModel(id.HasValue ? reservation.ReservationNumber : "New reservation", reservation, entity => SaveAndRefreshAsync(entity, value => _service.SaveAsync((Reservation)value)));
        await editor.InitializeAsync(_lookups, _masters);
        editor.Hint = "Choose the activity, date and number of participants. Fields marked * are required.";
        editor.ReferenceLabel = "Reservation number";
        editor.ReferenceValue = id.HasValue ? reservation.ReservationNumber : "Assigned automatically when saved (RES-YYYY-000001)";
        editor.Summary = id.HasValue ? await _service.PaymentSummaryAsync(id.Value) : "Reservation number will be generated when saved.";
        editor.Payments = id.HasValue ? await _service.PaymentsAsync(id.Value) : [];
        return editor;
    }
    public Task EditAsync(int? id) => EditAsync(id, null);
    public async Task EditAsync(int? id, DateTime? activityDate)
    {
        var editor = await CreateEditorAsync(id, activityDate);
        _dialogs.Edit(editor);
    }
    public ReservationDetailViewModel CreateDetail(int id) => new(id, _service, _dialogs, EditAsync, _payment);
    public async Task OpenDetailsAsync(int id)
    {
        await _dialogs.ShowScreenAsync(CreateDetail(id));
        await RefreshAsync();
    }
}
