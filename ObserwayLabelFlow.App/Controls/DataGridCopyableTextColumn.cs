using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ObserwayLabelFlow.App.Controls;

/// <summary>
/// Read-only TextBox cells so users can select partial text and copy with Ctrl+C.
/// </summary>
public class DataGridCopyableTextColumn : DataGridTextColumn
{
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        => CreateBoundTextBox();

    protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        => CreateBoundTextBox();

    private TextBox CreateBoundTextBox()
    {
        var textBox = new TextBox
        {
            FocusVisualStyle = null,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        if (Application.Current.TryFindResource("DataGridCopyableTextBox") is Style style)
            textBox.Style = style;

        if (Binding is not null)
            textBox.SetBinding(TextBox.TextProperty, CloneBindingOneWay(Binding));

        textBox.SetBinding(
            TextBox.ForegroundProperty,
            new Binding(nameof(Foreground))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1),
            });

        return textBox;
    }

    private static BindingBase CloneBindingOneWay(BindingBase source)
    {
        if (source is not Binding binding)
            return source;

        return new Binding
        {
            Path = binding.Path,
            Mode = BindingMode.OneWay,
            StringFormat = binding.StringFormat,
            Converter = binding.Converter,
            ConverterParameter = binding.ConverterParameter,
            FallbackValue = binding.FallbackValue,
            TargetNullValue = binding.TargetNullValue,
            ConverterCulture = binding.ConverterCulture,
        };
    }
}
