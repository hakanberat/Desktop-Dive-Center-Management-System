using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DiveCenterManager.Converters;

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value?.ToString() switch
        {
            "Confirmed" or "Completed" or "Available" => "StatusCompleteBrush",
            "Cancelled" or "NoShow" or "Retired" => "StatusCancelledBrush",
            "InProgress" or "InUse" => "StatusRunningBrush",
            _ => "StatusPendingBrush"
        };
        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.DimGray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException("Status colors are display only.");
}
