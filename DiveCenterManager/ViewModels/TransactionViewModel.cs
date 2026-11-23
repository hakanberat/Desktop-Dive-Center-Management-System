using System.Globalization;
using System.Collections.ObjectModel;
using DiveCenterManager.Commands;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class TransactionViewModel : ScreenViewModel
{
    private readonly TransactionService _service;
    private readonly LookupService _lookups;
    private readonly MasterDataService _masters;
    private readonly DialogService _dialogs;
    public ObservableCollection<CurrencyBalance> Balances { get; } = [];
    public AsyncRelayCommand EditCommand { get; }
    public TransactionViewModel(TransactionService service, LookupService lookups, MasterDataService masters, DialogService dialogs)
    {
        _service = service; _lookups = lookups; _masters = masters; _dialogs = dialogs;
        Title = "Transactions"; Subtitle = "Cash income and expenses. Each currency has its own balance; all filters apply to both the table and balances.";
        Columns = EntitySchema.Columns(typeof(Transaction));
        Filters.Add(new FieldViewModel("Year", "Year", typeof(int?), DateTime.Today.Year));
        Filters.Add(new FieldViewModel("Month", "Month", typeof(int?), null, lookup: true) { Choices = [new Choice(null, "All months"), .. Enumerable.Range(1, 12).Select(x => new Choice(x, CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(x)))] });
        Filters.Add(new FieldViewModel("Type", "Type", typeof(TransactionType?), null));
        Filters.Add(new FieldViewModel("CurrencyId", "Currency", typeof(int?), null, lookup: true));
        Filters.Add(new FieldViewModel("CategoryId", "Category", typeof(int?), null, lookup: true));
        Filters.Add(new FieldViewModel("ActivityId", "Activity", typeof(int?), null, lookup: true));
        Filters.Add(new FieldViewModel("Search", "Search", typeof(string), null));
        Filters.Add(new FieldViewModel("From", "Date from", typeof(DateTime?), null));
        Filters.Add(new FieldViewModel("To", "Date to", typeof(DateTime?), null));
        Filter("Type").Choices[0] = new Choice(null, "All types");
        Action("Add Income", () => EditAsync(new Transaction { TransactionType = TransactionType.Income }));
        Action("Add Expense", () => EditAsync(new Transaction { TransactionType = TransactionType.Expense }));
        EditCommand = new AsyncRelayCommand(async () => await EditAsync(await _service.GetAsync(((Transaction)Selected!).Id)), Fail, () => Selected is Transaction);
        Actions.Add(new ActionItem("Edit", EditCommand));
        Action("Delete", async () =>
        {
            var record = await _service.GetAsync(((Transaction)Selected!).Id);
            if (_dialogs.Confirm($"Permanently delete this {record.TransactionType.ToString().ToLowerInvariant()} transaction?\n\n{record.TransactionDate:dd MMM yyyy} · {record.Amount:#,##0.00##########################} {record.Currency?.Code}\n{record.Description}\n\nThis changes the cash balance and cannot be undone."))
            { await _service.DeleteAsync(record.Id, true); await RefreshAsync(); }
        }, true);
    }
    public override async Task LoadAsync()
    {
        foreach (var key in new[] { "CurrencyId", "CategoryId", "ActivityId" }) Filter(key).Choices = [new Choice(null, "All"), .. await _lookups.GetAsync(key, includeInactive: true)];
        var filter = new TransactionFilter(Filter("Year").Read() as int?, Filter("Month").SelectedValue as int?, Filter("Type").SelectedValue as TransactionType?, Filter("CurrencyId").SelectedValue as int?, Filter("CategoryId").SelectedValue as int?, Filter("ActivityId").SelectedValue as int?, SearchText, Filter("From").Date, Filter("To").Date);
        var snapshot = await _service.GetSnapshotAsync(filter);
        SetRows(snapshot.Transactions);
        Balances.Clear();
        Cards.Clear();
        foreach (var balance in snapshot.Balances)
        {
            Balances.Add(balance);
            Cards.Add(new MetricCard(balance.Code + " · filtered balance", balance.Balance.ToString("#,##0.00##########################"), $"Income {balance.Income:#,##0.00##########################}   /   Expense {balance.Expense:#,##0.00##########################}"));
        }
    }
    public Task<EditorViewModel> CreatePaymentEditorAsync(Reservation reservation) => CreateEditorAsync(new Transaction
        { TransactionType = TransactionType.Income, ReservationId = reservation.Id, ActivityId = reservation.ActivityId, PersonCount = reservation.NumberOfParticipants });
    public async Task AddPaymentAsync(Reservation reservation) { _dialogs.Edit(await CreatePaymentEditorAsync(reservation)); }
    public async Task<EditorViewModel> CreateEditorAsync(Transaction transaction)
    {
        var editor = new EditorViewModel(transaction.Id == 0 ? "Add " + transaction.TransactionType.ToString().ToLowerInvariant() : "Edit cash transaction", transaction, entity => SaveAndRefreshAsync(entity, value => _service.SaveAsync((Transaction)value)));
        await editor.InitializeAsync(_lookups, _masters);
        editor.Hint = "Enter a positive amount in one currency. Use separate expense and income entries for currency exchanges.";
        editor.Summary = transaction.Id == 0 ? "Enter the actual cash received or paid. Amount is not calculated from the activity's current default price."
            : $"Created {transaction.CreatedAt.ToLocalTime():g}. Last updated: {transaction.UpdatedAt?.ToLocalTime().ToString("g") ?? "Never"}.";
        var categories = (await _masters.ListAsync(typeof(TransactionCategory))).Cast<TransactionCategory>().ToList();
        var categoryField = editor.Fields.Single(x => x.Key == "CategoryId");
        var typeField = editor.Fields.Single(x => x.Key == "TransactionType");
        void RefreshCategories()
        {
            var selected = categoryField.SelectedValue;
            var type = typeField.SelectedValue as TransactionType?;
            categoryField.Choices = [new Choice(null, "— None —"), .. categories
                .Where(x => (x.IsActive || x.Id == transaction.CategoryId) && (x.Type == CategoryType.Both || x.Type.ToString() == type?.ToString()))
                .Select(x => new Choice(x.Id, x.Name + (x.IsActive ? "" : " (inactive)")))];
            categoryField.SelectedValue = categoryField.Choices.Any(x => Equals(x.Value, selected)) ? selected : null;
        }
        RefreshCategories(); typeField.ValueChanged += (_, _) => RefreshCategories();
        return editor;
    }
    private async Task EditAsync(Transaction transaction) { _dialogs.Edit(await CreateEditorAsync(transaction)); }
}
