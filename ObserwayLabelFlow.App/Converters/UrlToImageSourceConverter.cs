using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ObserwayLabelFlow.App.Converters;

public sealed class UrlToImageSourceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
            return Binding.DoNothing;

        try
        {
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile))
            {
                return Binding.DoNothing;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();
            if (image.CanFreeze)
                image.Freeze();
            return image;
        }
        catch
        {
            return Binding.DoNothing;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
