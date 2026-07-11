using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using ObserwayLabelFlow.App.Services;

namespace ObserwayLabelFlow.App.Views;

public partial class ProductImagePreviewWindow : Window
{
    private double _zoom = 1d;
    private bool _fitPending;

    private const double MinZoom = 0.25d;
    private const double MaxZoom = 4d;
    private const double ZoomStep = 1.25d;

    private ProductImagePreviewWindow(string imageUrl, string? title)
    {
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(title)
            ? Application.Current.TryFindResource("Loc_ProductImagePreviewTitle") as string ?? "Product image"
            : title;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        Loaded += (_, _) =>
        {
            if (TryLoadImage(imageUrl))
            {
                _fitPending = true;
                FitPreviewToViewport();
                return;
            }

            if (Application.Current is App app)
            {
                var localization = app.Services.GetRequiredService<ILocalizationService>();
                app.Services.GetRequiredService<IAppDialogService>().Show(
                    AppDialogKind.Error,
                    localization.Get("ProductImagePreviewTitle"),
                    localization.Get("ProductImageLoadFailed"),
                    Owner ?? Application.Current.MainWindow);
            }

            Close();
        };
    }

    public static void Show(string imageUrl, string? title, Window? owner)
    {
        var window = new ProductImagePreviewWindow(imageUrl, title);
        if (owner is not null)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        window.ShowDialog();
    }

    private bool TryLoadImage(string url)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(url, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            PreviewImage.Source = image;
            return true;
        }
        catch
        {
            PreviewImage.Source = null;
            return false;
        }
    }

    private void ApplyZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        PreviewScale.ScaleX = _zoom;
        PreviewScale.ScaleY = _zoom;
        ZoomPercentText.Text = $"{(int)Math.Round(_zoom * 100)}%";
        PreviewImage.InvalidateMeasure();
        PreviewScroll.InvalidateMeasure();
    }

    private void FitPreviewToViewport()
    {
        if (PreviewImage.Source is not BitmapSource bitmap)
            return;

        var availableWidth = PreviewScroll.ViewportWidth;
        var availableHeight = PreviewScroll.ViewportHeight;
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            _fitPending = true;
            return;
        }

        _fitPending = false;
        var padding = 12d;
        var scale = Math.Min(
            (availableWidth - padding) / bitmap.PixelWidth,
            (availableHeight - padding) / bitmap.PixelHeight);

        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
            scale = 1d;

        ApplyZoom(scale);
    }

    private void ChangeZoom(double factor) => ApplyZoom(_zoom * factor);

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
        => ChangeZoom(ZoomStep);

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
        => ChangeZoom(1d / ZoomStep);

    private void ZoomFit_Click(object sender, RoutedEventArgs e)
        => FitPreviewToViewport();

    private void PreviewScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitPending)
            FitPreviewToViewport();
    }

    private void PreviewScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        e.Handled = true;
        var factor = e.Delta > 0 ? ZoomStep : 1d / ZoomStep;
        ChangeZoom(factor);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
