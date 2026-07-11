using System.Globalization;

namespace ObserwayLabelFlow.App.Services;

internal static class ProductMeasurementFormatter
{
    private const double KgToLbs = 2.2046226218;

    public static double ToPounds(double kilograms) => kilograms * KgToLbs;

    public static string FormatPounds(double kilograms)
        => ToPounds(kilograms).ToString("0.##", CultureInfo.InvariantCulture);
}
