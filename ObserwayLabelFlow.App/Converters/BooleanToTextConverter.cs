using System.Globalization;
using System.Windows.Data;

namespace ObserwayLabelFlow.App.Converters;

public sealed class BooleanToTextConverter : IValueConverter
{
    // ConverterParameter: "falseText|trueText"
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool b)
            return Binding.DoNothing;

        var p = parameter?.ToString() ?? string.Empty;
        var parts = p.Split('|');
        var falseText = parts.Length > 0 ? parts[0] : string.Empty;
        var trueText = parts.Length > 1 ? parts[1] : string.Empty;
        return b ? trueText : falseText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

