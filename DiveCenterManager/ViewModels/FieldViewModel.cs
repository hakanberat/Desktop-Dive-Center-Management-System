using System.Globalization;
using System.Reflection;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class FieldViewModel : ViewModelBase
{
    public string Key { get; }
    public string Label { get; }
    public string Kind { get; }
    public Type ValueType { get; }
    public bool IsRequired { get; }
    public bool IsLongText { get; }
    public bool NullableString { get; private set; } = true;
    private List<Choice> _choices = [];
    public List<Choice> Choices { get => _choices; set { Set(ref _choices, value); OnPropertyChanged(nameof(SelectedChoice)); } }
    public Choice? SelectedChoice { get => Choices.FirstOrDefault(x => Equals(x.Value, SelectedValue)); set => SelectedValue = value?.Value; }
    private string _text = "";
    public string Text { get => _text; set => Set(ref _text, value); }
    private object? _selectedValue;
    public object? SelectedValue { get => _selectedValue; set { if (Set(ref _selectedValue, value)) { OnPropertyChanged(nameof(SelectedChoice)); ValueChanged?.Invoke(this, EventArgs.Empty); } } }
    private DateTime? _date;
    public DateTime? Date { get => _date; set => Set(ref _date, value); }
    private bool _checked;
    public bool Checked { get => _checked; set => Set(ref _checked, value); }
    private string _error = "";
    public string Error { get => _error; set => Set(ref _error, value); }
    public event EventHandler? ValueChanged;
    public string InputError { get; set; } = "";

    public FieldViewModel(string key, string label, Type type, object? value, bool required = false, bool lookup = false)
    {
        Key = key; Label = label + (required ? " *" : ""); ValueType = type; IsRequired = required;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        IsLongText = key.Contains("Notes") || key == "Description";
        Kind = lookup || underlying.IsEnum ? "Choice" : underlying == typeof(bool) ? "Boolean" : underlying == typeof(DateTime) ? "Date" : "Text";
        if (underlying.IsEnum)
        {
            if (Nullable.GetUnderlyingType(type) != null) Choices.Add(new Choice(null, "— None —"));
            Choices.AddRange(Enum.GetValues(underlying).Cast<object>().Select(x => new Choice(x, EntitySchema.Words(x.ToString()!))));
        }
        SetValue(value);
    }
    public void SetValue(object? value)
    {
        InputError = ""; Error = "";
        SelectedValue = value;
        Date = value is DateTime date ? date : null;
        Checked = value is true;
        Text = value is TimeSpan time ? time.ToString(@"hh\:mm", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
    }
    public object? Read()
    {
        if (InputError.Length > 0) { Error = InputError; throw new System.ComponentModel.DataAnnotations.ValidationException(InputError); }
        Error = "";
        var type = Nullable.GetUnderlyingType(ValueType) ?? ValueType;
        try
        {
            if (Kind == "Choice") { if (SelectedValue == null && (IsRequired || Nullable.GetUnderlyingType(ValueType) == null && ValueType.IsValueType)) throw new FormatException("Select a value."); return SelectedValue; }
            if (Kind == "Date") { if (!Date.HasValue && Nullable.GetUnderlyingType(ValueType) == null) throw new FormatException("Select a date."); return Date?.Date; }
            if (Kind == "Boolean") return Checked;
            if (string.IsNullOrWhiteSpace(Text))
            {
                if (IsRequired) throw new FormatException("This field is required.");
                if (type == typeof(string)) return NullableString ? null : "";
                if (Nullable.GetUnderlyingType(ValueType) != null) return null;
                throw new FormatException("Enter a value.");
            }
            if (type == typeof(string)) return Text.Trim();
            if (type == typeof(TimeSpan))
            {
                if (!TimeSpan.TryParseExact(Text.Trim(), [@"h\:mm", @"hh\:mm"], CultureInfo.InvariantCulture, out var time) || time >= TimeSpan.FromDays(1)) throw new FormatException("Use HH:mm, for example 09:30.");
                return time;
            }
            return Convert.ChangeType(Text.Trim(), type, CultureInfo.CurrentCulture);
        }
        catch (Exception e) when (e is FormatException or OverflowException or InvalidCastException)
        {
            Error = e is FormatException && e.Message.StartsWith("Use ", StringComparison.Ordinal) ? e.Message : "Enter a valid " + Label.TrimEnd(' ', '*').ToLowerInvariant() + ".";
            throw new System.ComponentModel.DataAnnotations.ValidationException(Error);
        }
    }
    public static async Task<List<FieldViewModel>> ForEntityAsync(object entity, LookupService lookups)
    {
        var fields = new List<FieldViewModel>();
        foreach (var property in EntitySchema.EditableProperties(entity.GetType()))
        {
            var isLookup = property.Name.EndsWith("Id", StringComparison.Ordinal);
            var isParticipantCount = entity is Reservation && property.Name == nameof(Reservation.NumberOfParticipants);
            var field = new FieldViewModel(property.Name, EntitySchema.Label(property.Name), property.PropertyType, property.GetValue(entity), EntitySchema.Required(property) || isParticipantCount, isLookup || isParticipantCount);
            field.NullableString = new NullabilityInfoContext().Create(property).WriteState != NullabilityState.NotNull;
            if (isParticipantCount)
                field.Choices = Enumerable.Range(1, 10000).Select(count => new Choice(count, count.ToString(CultureInfo.CurrentCulture))).ToList();
            if (isLookup)
            {
                field.Choices = await lookups.GetAsync(property.Name, property.GetValue(entity));
                field.Choices.Insert(0, new Choice(null, field.IsRequired ? "— Select —" : "— None —"));
                if (Equals(field.SelectedValue, 0)) field.SelectedValue = null;
            }
            fields.Add(field);
        }
        return fields;
    }
}
