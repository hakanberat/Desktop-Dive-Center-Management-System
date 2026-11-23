using System.Globalization;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DiveCenterManager.ViewModels;

namespace DiveCenterManager.Views;

// UI-only parsing feedback prevents an invalid typed date from silently leaving the old value.
public sealed class ValidatedDatePicker : DatePicker
{
    private DatePickerTextBox? _textBox;
    public ValidatedDatePicker()
    {
        SetResourceReference(StyleProperty, typeof(DatePicker));
        DateValidationError += (_, e) => { e.ThrowException = false; ValidateText(); };
    }
    public override void OnApplyTemplate()
    {
        if (_textBox != null) _textBox.TextChanged -= OnTextChanged;
        base.OnApplyTemplate();
        _textBox = GetTemplateChild("PART_TextBox") as DatePickerTextBox;
        if (_textBox != null) _textBox.TextChanged += OnTextChanged;
    }
    private void OnTextChanged(object sender, TextChangedEventArgs e) => ValidateText();
    private void ValidateText()
    {
        if (DataContext is not FieldViewModel field || _textBox == null) return;
        var text = _textBox.Text.Trim();
        field.InputError = text.Length == 0 || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out _) ? "" : "Enter a valid date for " + field.Label.TrimEnd(' ', '*') + ".";
        field.Error = field.InputError;
    }
}
