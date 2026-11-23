using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DiveCenterManager.Commands;
using DiveCenterManager.Data;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;
using DiveCenterManager.ViewModels;
using DiveCenterManager.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Activity = DiveCenterManager.Models.Activity;

namespace DiveCenterManager.Diagnostics;

public sealed partial class AcceptanceRunner
{
    public async Task RunPhase6Async()
    {
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        var cash = services.GetRequiredService<TransactionService>();
        var currencies = services.GetRequiredService<CurrencyService>();
        var categories = services.GetRequiredService<TransactionCategoryService>();
        var masters = services.GetRequiredService<MasterDataService>();
        var reservations = services.GetRequiredService<ReservationService>();
        var day = new DateTime(DateTime.Today.Year, 5, 17);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Check(!await db.Transactions.AnyAsync() && !await db.Reservations.AnyAsync(), "PHASE 6 uses isolated SQLite without seeded cash movements");
            Check(!db.Database.HasPendingModelChanges(), "Finance model matches the existing migration snapshot");
            var model = db.Model.FindEntityType(typeof(Transaction))!;
            Check(model.FindProperty("Amount") is { ClrType: not null } amount && amount.ClrType == typeof(decimal) && amount.GetColumnType() == "TEXT", "EF Amount uses decimal and SQLite TEXT storage");
            Check(model.GetForeignKeys().Count() == 4 && model.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "Currency/category/activity/reservation relationships preserve referenced records");
        }
        var money = (await currencies.ListAsync()).ToDictionary(x => x.Code);
        Check(money.Keys.Order().SequenceEqual(new[] { "EUR", "GBP", "TRY", "USD" }) && money.Values.All(x => x.IsActive), "Fresh seed contains active TRY, EUR, USD and GBP");
        var currencyVm = services.GetRequiredService<CurrencyViewModel>();
        var currencyEditor = await currencyVm.CreateEditorAsync(true);
        SetOperationField(currencyEditor, "Code", " cad "); SetOperationField(currencyEditor, "Name", "Canadian Dollar"); SetOperationField(currencyEditor, "Symbol", "C$");
        await currencyEditor.SaveCommand.ExecuteAsync();
        var cad = (Currency)currencyEditor.Entity;
        Check(currencyEditor.Error.Length == 0 && cad.Id > 0 && cad.Code == "CAD", "Currency form creates and normalizes an additional currency");
        await RejectAsync(() => currencies.SaveAsync(new Currency { Code = "cad", Name = "Duplicate", Symbol = "$" }), "Duplicate currency codes are rejected");
        var categoryVm = services.GetRequiredService<TransactionCategoryViewModel>();
        var categoryEditor = await categoryVm.CreateEditorAsync(true);
        SetOperationField(categoryEditor, "Name", "Manual exchange"); SetOperationField(categoryEditor, "Type", CategoryType.Both);
        await categoryEditor.SaveCommand.ExecuteAsync(); var exchange = (TransactionCategory)categoryEditor.Entity;
        Check(categoryEditor.Error.Length == 0 && exchange.Id > 0, "Category form creates a Both category");
        var incomeCategory = (await categories.ListAsync()).Single(x => x.Name == "Diving");
        var expenseCategory = (await categories.ListAsync()).Single(x => x.Name == "Fuel");
        var activity = new Activity { Name = "Guided Dalış", ActivityType = ActivityType.GuidedDive, DefaultCurrencyId = money["EUR"].Id, DefaultPrice = 75m };
        await masters.SaveAsync(activity);
        var booking = Draft(activity.Id, day); await reservations.SaveAsync(booking);
        var unrelated = Draft(activity.Id, day); await reservations.SaveAsync(unrelated);
        var main = services.GetRequiredService<MainViewModel>();
        await main.OpenAsync(main.Navigation.Single(x => x.Title == "Transactions")); var vm = (TransactionViewModel)main.Current!;
        Check(vm.Message.Length == 0 && new[] { "Add Income", "Add Expense", "Edit", "Delete" }.All(label => vm.Actions.Any(x => x.Label == label)) && !vm.EditCommand.CanExecute(null), "Transactions navigation provides the requested toolbar with selection-aware Edit");
        var editor = await vm.CreatePaymentEditorAsync(booking);
        var paidEur = (Transaction)editor.Entity;
        Check(paidEur.ReservationId == booking.Id && paidEur.ActivityId == activity.Id && paidEur.PersonCount == booking.NumberOfParticipants && paidEur.Amount == 0 && paidEur.CurrencyId == 0, "Reservation payment prefills booking context without calculating an amount or conversion");
        Check(new[] { "TransactionDate", "TransactionType", "Amount", "CurrencyId", "CategoryId", "ActivityId", "ReservationId", "PersonCount", "DiveCount", "Description" }.All(key => editor.Fields.Any(x => x.Key == key))
            && editor.Fields.All(x => x.Key is not "CreatedAt" and not "UpdatedAt"), "Cash form has all editable fields and keeps audit dates automatic");
        var categoryField = editor.Fields.Single(x => x.Key == "CategoryId");
        Check(categoryField.Choices.Any(x => Equals(x.Value, incomeCategory.Id)) && categoryField.Choices.Any(x => Equals(x.Value, exchange.Id)) && categoryField.Choices.All(x => !Equals(x.Value, expenseCategory.Id)), "Income picker includes Income/Both categories only");
        SetOperationField(editor, "CategoryId", incomeCategory.Id); SetOperationField(editor, "TransactionType", TransactionType.Expense);
        Check(categoryField.SelectedValue == null && categoryField.Choices.Any(x => Equals(x.Value, expenseCategory.Id)), "Switching type clears an incompatible category and updates choices");
        SetOperationField(editor, "TransactionType", TransactionType.Income); SetOperationField(editor, "CategoryId", incomeCategory.Id);
        SetOperationField(editor, "TransactionDate", day.AddHours(13)); SetOperationField(editor, "CurrencyId", money["EUR"].Id);
        SetOperationField(editor, "Amount", 200m); SetOperationField(editor, "DiveCount", 2); SetOperationField(editor, "Description", "Reservation deposit");
        await editor.SaveCommand.ExecuteAsync();
        Check(editor.Error.Length == 0 && paidEur.Id > 0 && paidEur.TransactionDate == day && paidEur.UpdatedAt == null, "WPF Save persists positive income, date-only value and new-record audit fields");
        var paidTry = new Transaction { TransactionDate = day, Amount = 5000m, CurrencyId = money["TRY"].Id, CategoryId = incomeCategory.Id, ActivityId = activity.Id, ReservationId = booking.Id, Description = "Reservation payment TRY" };
        await cash.SaveAsync(paidTry);
        var exchangeOut = new Transaction { TransactionDate = day, TransactionType = TransactionType.Expense, Amount = 70m, CurrencyId = money["EUR"].Id, CategoryId = exchange.Id, Description = "Currency exchange out" };
        await cash.SaveAsync(exchangeOut);
        var period = new TransactionFilter(From: day, To: day.AddDays(1));
        var balances = (await cash.BalancesAsync(period)).ToDictionary(x => x.Code);
        Check(balances["EUR"] is { Income: 200, Expense: 70, Balance: 130 } && balances["TRY"].Balance == 5000, "70 EUR expense affects only EUR and creates no TRY counterpart");
        var exchangeIn = new Transaction { TransactionDate = day.AddDays(1), Amount = 1785m, CurrencyId = money["TRY"].Id, CategoryId = exchange.Id, Description = "Currency exchange in" };
        await cash.SaveAsync(exchangeIn);
        balances = (await cash.BalancesAsync(period)).ToDictionary(x => x.Code);
        Check(balances["TRY"] is { Income: 6785, Expense: 0, Balance: 6785 } && balances["EUR"].Balance == 130, "1785 TRY income is a separate explicit cash movement with no conversion");
        var usdA = new Transaction { TransactionDate = day, CurrencyId = money["USD"].Id, Amount = .1m, Description = "Small receipt A" };
        var usdB = new Transaction { TransactionDate = day, CurrencyId = money["USD"].Id, Amount = .2m, Description = "Small receipt B" };
        var gbp = new Transaction { TransactionDate = day, CurrencyId = money["GBP"].Id, TransactionType = TransactionType.Expense, Amount = 5m, CategoryId = expenseCategory.Id, Description = "Fuel cash" };
        var earlier = new Transaction { TransactionDate = day.AddMonths(-1), CurrencyId = money["EUR"].Id, Amount = 10m, ReservationId = unrelated.Id };
        var prior = new Transaction { TransactionDate = day.AddYears(-1), CurrencyId = money["EUR"].Id, Amount = 999m };
        foreach (var row in new[] { usdA, usdB, gbp, earlier, prior }) await cash.SaveAsync(row);
        balances = (await cash.BalancesAsync(period)).ToDictionary(x => x.Code);
        Check(balances["USD"].Balance == .3m && balances["GBP"] is { Income: 0, Expense: 5, Balance: -5 } && balances["CAD"].Balance == 0 && balances.Count == 5, "Decimal sums are exact, expenses stay positive, and currencies have independent balances including zero");

        async Task ExpectRows(TransactionFilter filter, string name, params int[] expected)
        {
            var snapshot = await cash.GetSnapshotAsync(filter);
            Check(snapshot.Transactions.Select(x => x.Id).Order().SequenceEqual(expected.Order()), name);
        }
        await ExpectRows(new(Year: day.Year, Month: day.Month), "Year and month combine", paidEur.Id, paidTry.Id, exchangeOut.Id, exchangeIn.Id, usdA.Id, usdB.Id, gbp.Id);
        await ExpectRows(new(Type: TransactionType.Expense), "Expense type filter", exchangeOut.Id, gbp.Id);
        await ExpectRows(new(CurrencyId: money["USD"].Id), "Currency filter", usdA.Id, usdB.Id);
        await ExpectRows(new(CategoryId: exchange.Id), "Category filter", exchangeOut.Id, exchangeIn.Id);
        await ExpectRows(new(ActivityId: activity.Id), "Activity filter", paidEur.Id, paidTry.Id);
        await ExpectRows(new(Search: "  DEPOSIT  "), "Trimmed case-insensitive description search", paidEur.Id);
        await ExpectRows(new(Search: "MANUAL EXCHANGE"), "Category-name search", exchangeOut.Id, exchangeIn.Id);
        await ExpectRows(new(Search: "GUIDED"), "Activity-name search", paidEur.Id, paidTry.Id);
        await ExpectRows(new(ReservationId: booking.Id), "Reservation filter excludes unrelated cash", paidEur.Id, paidTry.Id);
        await ExpectRows(new(day.Year, day.Month, TransactionType.Income, money["EUR"].Id, incomeCategory.Id, activity.Id, "deposit", day, day, booking.Id), "Every filter combines on the same query", paidEur.Id);
        await ExpectRows(new(From: day.AddHours(22), To: day.AddHours(1), CurrencyId: money["TRY"].Id), "Date bounds use inclusive days and do not leak the following day", paidTry.Id);
        Check((await cash.BalancesAsync(new(From: day, To: day, CurrencyId: money["TRY"].Id))).Single().Balance == 5000
            && (await cash.BalancesAsync(new(Year: day.Year, Month: day.Month, CurrencyId: money["EUR"].Id))).Single().Balance == 130, "Date/year/month filters apply to balances as well as rows");
        Check((await cash.BalancesAsync(new(Type: TransactionType.Expense, CategoryId: exchange.Id))).Single(x => x.Code == "EUR").Balance == -70, "Non-date filters also apply consistently to filtered balances");
        Check((await cash.ListAsync(new(To: DateTime.MaxValue))).Count == 9, "Maximum end date does not overflow");
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            await ExpectRows(new(Search: "DIVING"), "ASCII uppercase search works under Turkish locale", paidEur.Id, paidTry.Id);
            await ExpectRows(new(Search: "DALIŞ"), "Unicode activity search works under Turkish locale", paidEur.Id, paidTry.Id);
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }

        await VerifyPhase6MoneyAndValidationAsync(cash, vm, paidEur.Id, money["EUR"].Id, cad.Id, expenseCategory.Id);
        var corrected = await cash.GetAsync(paidEur.Id); var created = corrected.CreatedAt;
        corrected.Amount = 201.25m; corrected.CreatedAt = day.AddYears(-5); corrected.UpdatedAt = day.AddYears(5); await cash.SaveAsync(corrected);
        Check(corrected.CreatedAt == created && corrected.UpdatedAt.HasValue && corrected.UpdatedAt.Value <= DateTime.UtcNow && (await cash.GetAsync(corrected.Id)).Amount == 201.25m, "Editing persists amount and server-controlled audit dates");
        corrected.Amount = 200m; await cash.SaveAsync(corrected);
        var otherCurrency = await cash.GetAsync(exchangeIn.Id); otherCurrency.Description = "Reviewed independent exchange receipt"; await cash.SaveAsync(otherCurrency);
        Check((await cash.GetAsync(exchangeOut.Id)).Amount == 70, "Editing one exchange entry never changes the independent other currency entry");
        var detailVm = ((ReservationViewModel)main.Navigation.Single(x => x.Title == "Reservations").Create()).CreateDetail(booking.Id);
        await detailVm.RefreshAsync();
        Check(detailVm.Payments.Count == 2 && detailVm.Payments.All(x => x.ReservationId == booking.Id) && detailVm.PaymentSummary.Contains("EUR") && detailVm.PaymentSummary.Contains("TRY")
            && !detailVm.PaymentSummary.Contains("USD") && detailVm.Actions.Any(x => x.Label == "Add reservation payment"), "Reservation details show linked cash records and separate currency summaries with Add payment navigation");
        Check(!detailVm.PaymentSummary.Contains("outstanding", StringComparison.OrdinalIgnoreCase), "No outstanding debt is inferred from current catalog prices or other currencies");
        await reservations.CancelAsync(booking.Id);
        Check((await reservations.PaymentsAsync(booking.Id)).Count == 2, "Reservation cancellation retains cash history");

        await VerifyPhase6CatalogHistoryAsync(vm, cash, currencies, categories, money["EUR"], incomeCategory, exchange, corrected.Id, day);
        await vm.ClearFiltersCommand.ExecuteAsync();
        Check(vm.Rows.Count == 9 && vm.Filters.All(x => x.Kind == "Choice" ? x.SelectedValue == null : x.Kind == "Date" ? x.Date == null : x.Text.Length == 0), "Clear Filters resets year, month, type, references, search and date bounds");
        SetOperationFieldForScreen(vm, "Year", day.Year); SetOperationFieldForScreen(vm, "Month", day.Month); SetOperationFieldForScreen(vm, "CurrencyId", money["EUR"].Id); await vm.RefreshAsync();
        Check(vm.Rows.Count == 2 && vm.Balances.Single().Balance == 130, "WPF filters and balance table use the same snapshot");
        SetOperationFieldForScreen(vm, "Year", "invalid"); await vm.RefreshCommand.ExecuteAsync();
        Check(vm.Message.Length > 0, "Invalid year text produces a visible filter error");
        await vm.ClearFiltersCommand.ExecuteAsync();

        var folder = Path.Combine(paths.DataDirectory, "phase6-screenshots"); Directory.CreateDirectory(folder);
        var listener = new BindingListener(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            await main.OpenAsync(main.Navigation.Single(x => x.Title == "Transactions")); vm = (TransactionViewModel)main.Current!;
            SetOperationFieldForScreen(vm, "Month", day.Month); await vm.RefreshAsync(); vm.Selected = vm.Rows.Cast<Transaction>().Single(x => x.Id == paidEur.Id);
            var view = new TransactionsView { DataContext = vm };
            await RenderAsync(view, Path.Combine(folder, "Transactions.png"), 1180, 850);
            Check(view.FindName("Records") is RecordGrid { Columns.Count: 10 }, "Native transaction grid contains all ten requested columns");
            var shell = new MainWindow(main); var content = (FrameworkElement)shell.Content; shell.Content = null; content.DataContext = main;
            await RenderAsync(content, Path.Combine(folder, "Transactions-Minimum-Window.png"), 1100, 720);
            var cashGrid = VisualDescendants(content).OfType<RecordGrid>().Single(x => AutomationProperties.GetName(x) == "Cash movement records");
            Check(cashGrid.ActualHeight >= 120, "Transaction rows remain usable at the minimum shell size");
            await RenderPhase4EditorAsync(await vm.CreateEditorAsync(await cash.GetAsync(paidEur.Id)), folder, "Transaction-Editor.png", false);
            await detailVm.RefreshAsync();
            var reservationView = new ReservationDetailView { DataContext = detailVm };
            await RenderAsync(reservationView, Path.Combine(folder, "Reservation-Details.png"), 1200, 1000);
            ((Border)reservationView.Parent).Child = null;
            VisualDescendants(reservationView).OfType<ScrollViewer>().First().ScrollToEnd();
            await RenderAsync(reservationView, Path.Combine(folder, "Reservation-Cash.png"), 1200, 780);
            await currencyVm.RefreshAsync(); await categoryVm.RefreshAsync();
            await RenderAsync(new CurrencyView { DataContext = currencyVm }, Path.Combine(folder, "Currencies.png"), 1100, 800);
            await RenderAsync(new TransactionCategoryView { DataContext = categoryVm }, Path.Combine(folder, "Categories.png"), 1100, 800);
            await VerifyPhase6DateEntryAsync(vm, money["USD"].Id, folder);
            Check(listener.Errors.Count == 0, "Cash screens, reservation transactions, catalogs and editors render without WPF binding errors");
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            await File.WriteAllLinesAsync(Path.Combine(paths.DataDirectory, "phase6-binding-diagnostics.txt"), listener.Errors);
        }
        var rows = await cash.ListAsync(new());
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase6-expected.json"), JsonSerializer.Serialize(new Phase6Snapshot(booking.Id, paidEur.Id, created, day, rows.Select(x => x.Id).Order().ToArray())));
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase6-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SetOperationFieldForScreen(ScreenViewModel vm, string key, object? value) => vm.Filters.Single(x => x.Key == key).SetValue(value);

    private async Task VerifyPhase6MoneyAndValidationAsync(TransactionService cash, TransactionViewModel vm, int existingId, int eurId, int cadId, int expenseCategoryId)
    {
        foreach (var (name, mutate) in new (string, Action<Transaction>)[]
        {
            ("zero amount", x => x.Amount = 0), ("negative amount", x => x.Amount = -1), ("missing currency", x => x.CurrencyId = 0),
            ("unknown currency", x => x.CurrencyId = int.MaxValue), ("missing date", x => x.TransactionDate = default),
            ("negative person count", x => x.PersonCount = -1), ("negative dive count", x => x.DiveCount = -1),
            ("invalid type", x => x.TransactionType = (TransactionType)99), ("incompatible category", x => x.CategoryId = expenseCategoryId),
            ("unknown reservation", x => x.ReservationId = int.MaxValue), ("unknown activity", x => x.ActivityId = int.MaxValue),
            ("unknown category", x => x.CategoryId = int.MaxValue)
        })
        { var invalid = await cash.GetAsync(existingId); mutate(invalid); await RejectAsync(() => cash.SaveAsync(invalid), "Reject " + name); }
        foreach (var filter in new[] { new TransactionFilter(Year: 0), new(Year: 10000), new(Month: 0), new(Month: 13), new(Type: (TransactionType)99), new(From: DateTime.Today, To: DateTime.Today.AddDays(-1)) })
            await RejectAsync(() => cash.ListAsync(filter), "Invalid year/month/type/date range is rejected");
        var invalidEditor = await vm.CreateEditorAsync(new Transaction { CurrencyId = eurId });
        await invalidEditor.SaveCommand.ExecuteAsync();
        Check(invalidEditor.Error.Length > 0 && ((Transaction)invalidEditor.Entity).Id == 0, "Zero amount cannot be saved through the WPF editor");
        var factory = services.GetRequiredService<IDbContextFactory<DiveCenterDbContext>>();
        foreach (var value in new[] { .0000000000000000000000000001m, 1234567890123456789.123456789m })
        {
            var precise = new Transaction { CurrencyId = cadId, Amount = value, PersonCount = 0, DiveCount = 0 };
            await cash.SaveAsync(precise);
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.OpenConnectionAsync(); await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT typeof(Amount), Amount FROM Transactions WHERE Id = $id"; command.Parameters.Add(new SqliteParameter("$id", precise.Id));
                await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
                Check(reader.GetString(0) == "text" && decimal.Parse(reader.GetString(1), CultureInfo.InvariantCulture) == value && (await cash.GetAsync(precise.Id)).Amount == value,
                    "SQLite stores and round-trips exact decimal " + value.ToString(CultureInfo.InvariantCulture));
            }
            Check((await cash.BalancesAsync(new(CurrencyId: cadId))).Single().Balance == value, "Balance keeps exact decimal precision without conversion");
            await RejectAsync(() => cash.DeleteAsync(precise.Id, false), "Deletion requires explicit confirmation");
            Check((await cash.GetAsync(precise.Id)).Amount == value, "Unconfirmed deletion leaves the cash record intact");
            await cash.DeleteAsync(precise.Id, true);
            Check((await cash.BalancesAsync(new(CurrencyId: cadId))).Single().Balance == 0, "Confirmed deletion updates only that currency balance");
            await RejectAsync(() => cash.GetAsync(precise.Id), "Deleted transaction cannot be reopened");
        }
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "tr-TR", "en-US", "ro-RO" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var editor = await vm.CreateEditorAsync(new Transaction { CurrencyId = cadId }); SetOperationField(editor, "Amount", 1234.5678m);
                await editor.SaveCommand.ExecuteAsync(); var saved = (Transaction)editor.Entity;
                Check(editor.Error.Length == 0 && (await cash.GetAsync(saved.Id)).Amount == 1234.5678m, "Money editor preserves local decimal input in " + culture);
                await cash.DeleteAsync(saved.Id, true);
            }
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    private async Task VerifyPhase6CatalogHistoryAsync(TransactionViewModel vm, TransactionService cash, CurrencyService currencies, TransactionCategoryService categories, Currency eur, TransactionCategory income, TransactionCategory exchange, int id, DateTime day)
    {
        await currencies.SetActiveAsync(eur.Id, false); await categories.SetActiveAsync(income.Id, false);
        var historic = await cash.GetAsync(id); historic.Description = "Reviewed reservation deposit"; await cash.SaveAsync(historic);
        Check((await cash.GetAsync(id)) is { Currency.IsActive: false, Category.IsActive: false }, "Historical cash can be corrected while unchanged catalog references are inactive");
        await RejectAsync(() => cash.SaveAsync(new Transaction { CurrencyId = eur.Id, Amount = 1 }), "New cash cannot assign an inactive currency");
        var activeCurrency = (await currencies.ListAsync()).First(x => x.IsActive);
        await RejectAsync(() => cash.SaveAsync(new Transaction { CurrencyId = activeCurrency.Id, CategoryId = income.Id, Amount = 1 }), "New cash cannot assign an inactive category");
        Check((await cash.BalancesAsync(new(From: day, To: day))).Any(x => x.Code == "EUR" && x.Balance == 130), "Inactive currencies with filtered history remain visible in balances");
        Check((await cash.BalancesAsync(new(From: day.AddYears(5), CurrencyId: eur.Id))).Single() is { Code: "EUR", Balance: 0 }, "Explicit inactive currency filter still shows an empty zero balance");
        var changedCode = (Currency)EntityCopy.Copy(eur); changedCode.Code = "ZZZ";
        await RejectAsync(() => currencies.SaveAsync(changedCode), "Currency codes with cash history cannot be relabeled");
        var changedCategory = (TransactionCategory)EntityCopy.Copy(exchange); changedCategory.Type = CategoryType.Income;
        await RejectAsync(() => categories.SaveAsync(changedCategory), "Both category cannot become Income while referenced by expenses");
        exchange.Name = "Reviewed manual exchange"; await categories.SaveAsync(exchange);
        Check((await cash.ListAsync(new(CategoryId: exchange.Id))).All(x => x.Category!.Name == exchange.Name), "Renaming a referenced category preserves its transaction links");
        await services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        Check(!(await currencies.ListAsync()).Single(x => x.Id == eur.Id).IsActive, "Startup initialization preserves currency deactivation");
        await currencies.SetActiveAsync(eur.Id, true); await categories.SetActiveAsync(income.Id, true);
        var newCategory = new TransactionCategory { Name = "Added after screen load", Type = CategoryType.Both }; await categories.SaveAsync(newCategory);
        await vm.RefreshAsync();
        Check(vm.Filters.Single(x => x.Key == "CategoryId").Choices.Any(x => Equals(x.Value, newCategory.Id)), "Refresh picks up catalog changes made after the screen was opened");
        await categories.SetActiveAsync(newCategory.Id, false);
        Check((await categories.ListAsync()).Any(x => x.Id == newCategory.Id && !x.IsActive), "Category removal uses deactivation and retains the catalog record");
    }

    private async Task VerifyPhase6DateEntryAsync(TransactionViewModel vm, int currencyId, string folder)
    {
        var editor = await vm.CreateEditorAsync(new Transaction { CurrencyId = currencyId, Amount = 1 });
        var window = new EditorWindow { DataContext = editor }; var content = (FrameworkElement)window.Content; window.Content = null; content.DataContext = editor;
        await RenderAsync(content, Path.Combine(folder, "Income-New.png"), 880, 810);
        var picker = VisualDescendants(content).OfType<ValidatedDatePicker>().Single(x => x.DataContext is FieldViewModel { Key: "TransactionDate" });
        var input = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker); input.Text = "not-a-date";
        await editor.SaveCommand.ExecuteAsync();
        Check(editor.Error.Length > 0 && ((Transaction)editor.Entity).Id == 0, "Invalid text typed into the actual WPF DatePicker blocks a cash save");
    }

    public sealed record Phase6Snapshot(int ReservationId, int TransactionId, DateTime CreatedAt, DateTime Day, int[] TransactionIds);
    public async Task VerifyPhase6PersistenceAsync()
    {
        var expected = JsonSerializer.Deserialize<Phase6Snapshot>(await File.ReadAllTextAsync(Path.Combine(paths.DataDirectory, "phase6-expected.json")))!;
        var cash = services.GetRequiredService<TransactionService>(); var saved = await cash.GetAsync(expected.TransactionId);
        Check(saved.ReservationId == expected.ReservationId && saved.Currency?.Code == "EUR" && saved.Amount == 200 && saved.CreatedAt == expected.CreatedAt && saved.UpdatedAt.HasValue, "Restart preserves cash values, audit dates and reservation link");
        Check((await cash.ListAsync(new())).Select(x => x.Id).Order().SequenceEqual(expected.TransactionIds), "Restart preserves all transaction identities without duplicates");
        var balances = (await cash.BalancesAsync(new(From: expected.Day, To: expected.Day.AddDays(1)))).ToDictionary(x => x.Code);
        Check(balances["EUR"].Balance == 130 && balances["TRY"].Balance == 6785 && balances["USD"].Balance == .3m && balances["GBP"].Balance == -5, "Restart recomputes independent exact currency balances from SQLite");
        Check((await services.GetRequiredService<ReservationService>().PaymentsAsync(expected.ReservationId)).Count == 2, "Restart retains payments of the cancelled reservation");
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "phase6-persistence-results.json"), JsonSerializer.Serialize(new { passed = _passed.Count, checks = _passed }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
