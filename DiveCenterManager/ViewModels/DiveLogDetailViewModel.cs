using System.Collections.ObjectModel;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public record DiveLogDetailField(string Label, string Value);

public sealed class DiveLogDetailViewModel : ScreenViewModel
{
    private readonly int _id;
    private readonly LogbookService _service;
    private readonly LookupService _lookups;
    private readonly MasterDataService _masters;
    public ObservableCollection<DiveLogDetailField> Details { get; } = [];
    private string _notes = "";
    public string Notes { get => _notes; private set => Set(ref _notes, value); }

    public DiveLogDetailViewModel(int id, LogbookService service, LookupService lookups, MasterDataService masters, DialogService dialogs, Func<Task> editShared)
    {
        _id = id; _service = service; _lookups = lookups; _masters = masters;
        Title = "Dive log details";
        Subtitle = "Shared measurements apply to all divers. Blank personal depth and bottom time inherit the shared values.";
        Columns = EntitySchema.Columns(typeof(DiverDiveLog));
        Action("Edit shared / diver records", async () => { await editShared(); await RefreshAsync(); });
        Action("Edit selected diver", async () =>
        {
            dialogs.Edit(await CreatePersonalEditorAsync(((DiverDiveLog)Selected!).Id));
        }, true);
        Action("Verify selected diver", () => SetSelectedVerifiedAsync(true), true);
        Action("Clear verification", () => SetSelectedVerifiedAsync(false), true);
    }

    public async Task<EditorViewModel> CreatePersonalEditorAsync(int personalLogId)
    {
        var log = await _service.GetAsync(_id);
        var person = log.DiverLogs.Single(x => x.Id == personalLogId);
        var editor = new EditorViewModel($"Personal dive log - {person.ParticipantName}", person,
            input => SaveAndRefreshAsync(input, value => _service.SavePersonalAsync((DiverDiveLog)value)));
        await editor.InitializeAsync(_lookups, _masters);
        editor.Hint = "Save this diver's equipment, gas, buddy, personal measurements and verification.";
        editor.ReferenceLabel = "Shared dive information";
        editor.ReferenceValue = $"Log #{log.Id} | {log.Date:dd MMM yyyy} | {log.DiveSite?.Name}";
        editor.Summary = $"Blank personal depth and time use {log.MaxDepthMeters} m and {log.BottomTimeMinutes} min. Changes apply only to this diver.";
        return editor;
    }

    public async Task SetSelectedVerifiedAsync(bool verified)
    {
        if (Selected is not DiverDiveLog person) return;
        await _service.SetVerifiedAsync(_id, person.Id, verified);
        await RefreshAsync();
        Selected = Rows.Cast<DiverDiveLog>().SingleOrDefault(x => x.Id == person.Id);
    }

    public override async Task LoadAsync()
    {
        var log = await _service.GetAsync(_id);
        Title = $"Dive log #{log.Id}"; OnPropertyChanged(nameof(Title));
        Details.Clear();
        Add("Operation", $"#{log.DiveOperationId} - {log.DiveOperation?.Status}");
        Add("Reservation", log.DiveOperation?.Reservation?.ReservationNumber ?? "Manual operation");
        Add("Dive site", log.DiveSite?.Name);
        Add("Date", log.Date.ToString("dd MMM yyyy"));
        Add("Entry / exit", $"{log.EntryTime:hh\\:mm} - {log.ExitTime:hh\\:mm}");
        Add("Dive type", EntitySchema.Words(log.DiveType.ToString()));
        Add("Max depth (m)", log.MaxDepthMeters);
        Add("Bottom time (min)", log.BottomTimeMinutes);
        Add("Water temperature (C)", log.WaterTemperatureCelsius);
        Add("Visibility (m)", log.VisibilityMeters);
        Add("Current", log.CurrentCondition);
        Add("Weather", log.WeatherCondition);
        Notes = string.IsNullOrWhiteSpace(log.Notes) ? "No shared notes." : log.Notes;
        SetRows(log.DiverLogs.OrderBy(x => x.Id));
        void Add(string label, object? value) => Details.Add(new(label, string.IsNullOrWhiteSpace(value?.ToString()) ? "—" : value.ToString()!));
    }
}
