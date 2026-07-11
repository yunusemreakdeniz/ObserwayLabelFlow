using System.Windows;
using System.Windows.Controls;

namespace ObserwayLabelFlow.App.Infrastructure;

/// <summary>
/// Keeps DataGrid columns at fixed pixel widths so horizontal scrolling can engage.
/// </summary>
public static class DataGridColumnScrollAssist
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DataGridColumnScrollAssist),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid || e.NewValue is not true)
            return;

        grid.Loaded -= OnGridLoaded;
        grid.Loaded += OnGridLoaded;
        grid.SizeChanged -= OnGridSizeChanged;
        grid.SizeChanged += OnGridSizeChanged;

        if (grid.IsLoaded)
            LockColumnWidths(grid);
    }

    private static void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            LockColumnWidths(grid);
    }

    private static void OnGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is DataGrid grid)
            LockColumnWidths(grid);
    }

    private static void LockColumnWidths(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            var width = ResolveWidth(column);
            if (width <= 0)
                continue;

            column.MinWidth = Math.Max(column.MinWidth, width);
            column.Width = new DataGridLength(width, DataGridLengthUnitType.Pixel);
        }
    }

    private static double ResolveWidth(DataGridColumn column)
    {
        if (column.Width.IsAbsolute)
            return column.Width.Value;

        if (column.Width.IsAuto && column.ActualWidth > 0)
            return column.ActualWidth;

        if (column.MinWidth > 0)
            return column.MinWidth;

        return 0;
    }
}
