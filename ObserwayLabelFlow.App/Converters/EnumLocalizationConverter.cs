using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ObserwayLabelFlow.App.Converters;

public sealed class EnumLocalizationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return string.Empty;

        var resourceKey = $"Loc_{value.GetType().Name}_{value}";
        if (Application.Current?.Resources[resourceKey] is string localized)
            return localized;

        return value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
