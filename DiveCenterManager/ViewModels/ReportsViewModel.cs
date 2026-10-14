using System.Collections.ObjectModel;
using DiveCenterManager.Commands;
using DiveCenterManager.Helpers;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class ReportsViewModel : ScreenViewModel
{
    private readonly ReportService _service;
    private readonly LookupService _lookups;
    private readonly Dictionary<string, FieldViewModel> _fields = [];
    public FieldViewModel ReportField => _fields["Report"];
    public ObservableCollection<FieldViewModel> VisibleFilters { get; } = [];
    public ObservableCollection<ActivityCountRow> ActivityTotals { get; } = [];
    public List<ColumnSpec> ActivityColumns { get; } = [new("ActivityId", "Activity #"), new("Activity", "Activity"), new("Transactions", "Transactions"), new("Persons", "Participant total"), new("Dives", "Dive total")];
    public AsyncRelayCommand ResetFiltersCommand => ClearFiltersCommand;
    public ReportKind SelectedKind => ReportField.SelectedValue as ReportKind? ?? ReportKind.ReservationsByDate;
    public bool HasActivityTotals => SelectedKind == ReportKind.ActivityIncomeSummary && ActivityTotals.Count > 0;
    public string ReportHint => SelectedKind switch
    {
        ReportKind.MonthlySummary => "All 12 months of the selected year. Each currency has its own income, expense and balance; empty months show zero.",
        ReportKind.ActivityIncomeSummary => "Activity counts sum the person/dive counts recorded on income transactions. Income remains separate for each currency.",
        ReportKind.ParticipantCounts or ReportKind.ReservationsByActivity => "Reservation counts include all statuses. Participant totals exclude cancelled and no-show bookings.",
        ReportKind.DivesByDiveSite or ReportKind.DivesByInstructor => "Counts shared dive logs. Instructor means the operation's lead instructor; customer filtering limits the personal records counted.",
        ReportKind.CustomerDiveHistory => "Personal depth/time overrides take precedence. Instructor refers to the individual diver's log; temporary visitors are included.",
        ReportKind.LoggedDivesPerCustomer => "Period counts use actual personal logs. All-time totals include prior dives on the profile and all local logs.",
        >= ReportKind.IncomeByDate => "Cash movements in their original currencies, with payments linked to reservations.",
        ReportKind.DiveOperationsByDate => "All operation statuses in the period. Instructor refers to the lead instructor; activity filters apply to reservation-linked operations.",
        _ => "Reservations and participant counts in the selected activity-date range."
    };

    public ReportsViewModel(ReportService service, LookupService lookups)
    {
        _service = service; _lookups = lookups; Title = "Reports";
        Subtitle = "Operational activity and cash movement, queried from your saved records.";
        Add(new FieldViewModel("Report", "Report", typeof(ReportKind), ReportKind.ReservationsByDate));
        ReportField.Choices = ReportField.Choices.Where(x => x.Value is ReportKind kind && (kind <= ReportKind.NoShows || kind >= ReportKind.IncomeByDate)).ToList();
        Add(new FieldViewModel("From", "Date from", typeof(DateTime?), new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)));
        Add(new FieldViewModel("To", "Date to", typeof(DateTime?), DateTime.Today));
        Add(new FieldViewModel("Year", "Year", typeof(int), DateTime.Today.Year, required: true));
        foreach (var (key, label) in new[] { ("ActivityId", "Activity"), ("DiveSiteId", "Dive site"), ("InstructorId", "Instructor"), ("CustomerId", "Customer / diver"), ("CurrencyId", "Currency") })
            Add(new FieldViewModel(key, label, typeof(int?), null, lookup: true));
        ReportField.SelectedValue = ReportKind.ReservationsByDate;
        ReportField.ValueChanged += (_, _) =>
        {
            UpdateVisibleFilters(); SetRows([]); ActivityTotals.Clear(); Columns = []; Message = "Choose filters and run the report.";
            OnPropertyChanged(nameof(HasActivityTotals)); OnPropertyChanged(nameof(ReportHint));
        };
        UpdateVisibleFilters();
        void Add(FieldViewModel field) => _fields.Add(field.Key, field);
    }
    protected override async Task ClearFiltersAsync()
    {
        foreach (var field in _fields.Values.Where(x => x.Key != "Report")) field.SetValue(field.Key == "Year" ? DateTime.Today.Year : null);
        await RefreshAsync();
    }

    private void UpdateVisibleFilters()
    {
        VisibleFilters.Clear(); Filters.Clear(); Filters.Add(ReportField);
        var monthly = SelectedKind == ReportKind.MonthlySummary;
        var financial = SelectedKind >= ReportKind.IncomeByDate;
        foreach (var field in _fields.Values.Where(x => x.Key != "Report"))
        {
            if (field.Key == "CustomerId" || field.Key == "Year" && !monthly || field.Key is "From" or "To" && monthly
                || field.Key is "DiveSiteId" or "InstructorId" && financial || field.Key == "CurrencyId" && !financial) continue;
            VisibleFilters.Add(field); Filters.Add(field);
        }
    }

    public override async Task LoadAsync()
    {
        foreach (var field in VisibleFilters.Where(x => x.Key.EndsWith("Id", StringComparison.Ordinal)))
            field.Choices = [new Choice(null, "All"), .. await _lookups.GetAsync(field.Key, includeInactive: true)];
        var kind = SelectedKind;
        var monthly = kind == ReportKind.MonthlySummary; var financial = kind >= ReportKind.IncomeByDate;
        var from = monthly ? null : _fields["From"].Date; var to = monthly ? null : _fields["To"].Date;
        var year = monthly ? (int?)_fields["Year"].Read() : null;
        var filter = new ReportFilter(kind, from, to, _fields["ActivityId"].SelectedValue as int?, _fields["CustomerId"].SelectedValue as int?,
            financial ? null : _fields["DiveSiteId"].SelectedValue as int?, financial ? null : _fields["InstructorId"].SelectedValue as int?, year,
            financial ? _fields["CurrencyId"].SelectedValue as int? : null);
        var result = await _service.GetAsync(filter);
        Columns = ColumnsFor(kind);
        SetRows(result.Rows);
        ActivityTotals.Clear(); foreach (var row in result.ActivityTotals) ActivityTotals.Add(row);
        OnPropertyChanged(nameof(HasActivityTotals));
    }

    private static List<ColumnSpec> ColumnsFor(ReportKind kind)
    {
        const string money = "{0:#,##0.00##########################}";
        return kind switch
        {
            ReportKind.ReservationsByDate or ReportKind.CancelledReservations or ReportKind.NoShows =>
                [new("ReservationNumber", "Reservation"), new("Date", "Activity date", "{0:dd MMM yyyy}"), new("StartTime", "Time", "{0:hh\\:mm}"), new("Activity", "Activity"), new("Participants", "Participants"), new("Instructor", "Instructor"), new("DiveSite", "Dive site"), new("Boat", "Boat"), new("Status", "Status")],
            ReportKind.ReservationsByActivity => [new("GroupId", "Activity #"), new("Group", "Activity"), new("Reservations", "Reservations"), new("Participants", "Active/completed participants")],
            ReportKind.ParticipantCounts => [new("Group", "Date"), new("Reservations", "Reservations"), new("Participants", "Active/completed participants")],
            ReportKind.DiveOperationsByDate => [new("Id", "Operation #"), new("Date", "Date", "{0:dd MMM yyyy}"), new("StartTime", "Time", "{0:hh\\:mm}"), new("DiveSite", "Dive site"), new("Instructor", "Lead instructor"), new("Boat", "Boat"), new("DiveType", "Dive type"), new("Participants", "Participants"), new("Reservation", "Reservation"), new("Status", "Status")],
            ReportKind.DivesByDiveSite or ReportKind.DivesByInstructor => [new("GroupId", "Reference #"), new("Group", kind == ReportKind.DivesByInstructor ? "Lead instructor" : "Dive site"), new("Dives", "Logged dives"), new("Participants", "Diver records")],
            ReportKind.CustomerDiveHistory => [new("Customer", "Customer / visitor"), new("Date", "Date", "{0:dd MMM yyyy}"), new("DiveSite", "Dive site"), new("Instructor", "Instructor"), new("MaxDepthMeters", "Depth (m)"), new("BottomTimeMinutes", "Time (min)"), new("Gas", "Gas"), new("Buddy", "Buddy"), new("Verified", "Verified")],
            ReportKind.LoggedDivesPerCustomer => [new("CustomerId", "Customer #"), new("Customer", "Customer"), new("DivesInRange", "Center dives in period"), new("TotalLoggedDives", "All-time logged dives")],
            ReportKind.IncomeByDate or ReportKind.ExpenseByDate => [new("Date", "Date", "{0:dd MMM yyyy}"), new("Type", "Type"), new("Activity", "Activity"), new("Category", "Category"), new("Amount", "Amount", money), new("Currency", "Currency"), new("Persons", "Person count"), new("Dives", "Dive count"), new("Reservation", "Reservation"), new("Description", "Description")],
            ReportKind.MonthlySummary => [new("Year", "Year"), new("Group", "Month"), new("Currency", "Currency"), new("Transactions", "Transactions"), new("Income", "Income", money), new("Expense", "Expense", money), new("NetCash", "Balance", money)],
            ReportKind.ActivityIncomeSummary => [new("GroupId", "Activity #"), new("Group", "Activity"), new("Currency", "Currency"), new("Income", "Income", money)],
            ReportKind.IncomeByCategory => [new("GroupId", "Category #"), new("Group", "Category"), new("Currency", "Currency"), new("Transactions", "Transactions"), new("Income", "Income", money)],
            _ => [new("GroupId", "Category #"), new("Group", "Category"), new("Currency", "Currency"), new("Transactions", "Transactions"), new("Expense", "Expense", money)]
        };
    }
}
