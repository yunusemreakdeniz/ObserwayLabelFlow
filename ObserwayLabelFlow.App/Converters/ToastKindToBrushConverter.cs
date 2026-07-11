using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ObserwayLabelFlow.App.Services;

namespace ObserwayLabelFlow.App.Converters;

public sealed class ToastKindToAccentBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ToastKind kind)
            return Application.Current?.FindResource("AccentBrush") ?? Brushes.DodgerBlue;

        var key = kind switch
        {
            ToastKind.Success => "ToastSuccessBrush",
            ToastKind.Warning => "ToastWarningBrush",
            ToastKind.Error => "ToastErrorBrush",
            _ => "ToastInfoBrush"
        };

        return Application.Current?.FindResource(key) ?? Brushes.DodgerBlue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class ToastKindToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ToastKind kind)
            return "\uE946";

        return kind switch
        {
            ToastKind.Success => "\uE73E",
            ToastKind.Warning => "\uE7BA",
            ToastKind.Error => "\uE711",
            _ => "\uE946"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
