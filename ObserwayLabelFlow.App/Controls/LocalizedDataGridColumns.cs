using ObserwayLabelFlow.App.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ObserwayLabelFlow.App.Controls;

/// <summary>
/// DataGrid columns whose headers stay in sync when UI culture changes.
/// Uses HeaderTemplate + SetResourceReference instead of static Header strings.
/// </summary>
public class LocalizedDataGridTextColumn : DataGridTextColumn
{
    public LocalizedDataGridTextColumn()
    {
        if (Application.Current?.TryFindResource("DataGridCellTextBlock") is Style elementStyle)
            ElementStyle = elementStyle;
    }

    public static readonly DependencyProperty HeaderLocKeyProperty =
        DependencyProperty.Register(
            nameof(HeaderLocKey),
            typeof(string),
            typeof(LocalizedDataGridTextColumn),
            new PropertyMetadata(null, OnHeaderLocKeyChanged));

    public string? HeaderLocKey
    {
        get => (string?)GetValue(HeaderLocKeyProperty);
        set => SetValue(HeaderLocKeyProperty, value);
    }

    private static void OnHeaderLocKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LocalizedDataGridTextColumn column && e.NewValue is string key)
            column.ApplyLocalizedHeader(key);
    }

    private void ApplyLocalizedHeader(string key)
    {
        var resourceKey = key.StartsWith("Loc_", StringComparison.Ordinal) ? key : $"Loc_{key}";
        Header = null;
        HeaderTemplate = CreateHeaderTemplate(resourceKey);
    }

    internal static DataTemplate CreateHeaderTemplate(string key)
    {
        var locKey = key.StartsWith("Loc_", StringComparison.Ordinal) ? key[4..] : key;
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(FieldLabelAssist.LocKeyProperty, locKey);
        factory.SetValue(FieldLabelAssist.AppendColonProperty, true);
        factory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        factory.SetResourceReference(TextBlock.ForegroundProperty, "Text0Brush");
        factory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        return new DataTemplate { VisualTree = factory };
    }
}

public class LocalizedDataGridCheckBoxColumn : DataGridCheckBoxColumn
{
    public static readonly DependencyProperty HeaderLocKeyProperty =
        DependencyProperty.Register(
            nameof(HeaderLocKey),
            typeof(string),
            typeof(LocalizedDataGridCheckBoxColumn),
            new PropertyMetadata(null, OnHeaderLocKeyChanged));

    public string? HeaderLocKey
    {
        get => (string?)GetValue(HeaderLocKeyProperty);
        set => SetValue(HeaderLocKeyProperty, value);
    }

    private static void OnHeaderLocKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LocalizedDataGridCheckBoxColumn column && e.NewValue is string key)
            column.ApplyLocalizedHeader(key);
    }

    private void ApplyLocalizedHeader(string key)
    {
        var resourceKey = key.StartsWith("Loc_", StringComparison.Ordinal) ? key : $"Loc_{key}";
        Header = null;
        HeaderTemplate = LocalizedDataGridTextColumn.CreateHeaderTemplate(resourceKey);
    }
}
