using System.Globalization;
using System.Windows.Data;

namespace DiveCenterManager.Converters;

public sealed class TimeOfDayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is TimeSpan time ? time.ToString(@"hh\:mm", culture) : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException("Time labels are display only.");
}
