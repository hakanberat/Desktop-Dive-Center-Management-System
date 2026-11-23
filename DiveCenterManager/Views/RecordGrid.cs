using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DiveCenterManager.Helpers;
using DiveCenterManager.Converters;

namespace DiveCenterManager.Views;

// Presentation-only column construction keeps catalog and operational grids consistent.
public sealed class RecordGrid : DataGrid
{
    public static readonly DependencyProperty ColumnDefinitionsProperty = DependencyProperty.Register(nameof(ColumnDefinitions), typeof(IEnumerable), typeof(RecordGrid), new PropertyMetadata(null, OnColumnsChanged));
    public IEnumerable? ColumnDefinitions { get => (IEnumerable?)GetValue(ColumnDefinitionsProperty); set => SetValue(ColumnDefinitionsProperty, value); }
    public RecordGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        SelectionChanged += (_, _) =>
        {
            if (SelectedItem is { } selected) ScrollIntoView(selected);
        };
    }
    private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (RecordGrid)d;
        grid.Columns.Clear();
        if (e.NewValue is not IEnumerable columns) return;
        foreach (var column in columns.Cast<ColumnSpec>())
        {
            if (column.Path == "Status")
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding("Status") { Converter = new StatusBrushConverter() }));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                grid.Columns.Add(new DataGridTextColumn { Header = column.Header, Binding = new Binding(column.Path), ElementStyle = style, MinWidth = 110 });
                continue;
            }
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header,
                Binding = new Binding(column.Path) { StringFormat = column.Format, Mode = BindingMode.OneWay },
                MinWidth = column.Path == "Description" || column.Path == "Notes" ? 220 : 100,
                Width = DataGridLength.Auto,
                MaxWidth = column.Path == "Description" || column.Path == "Notes" || column.Path == "Value" ? 800 : 300
            });
        }
    }
}
