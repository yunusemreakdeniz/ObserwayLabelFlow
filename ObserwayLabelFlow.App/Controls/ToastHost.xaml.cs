using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using ObserwayLabelFlow.App.Services;
using ObserwayLabelFlow.App.ViewModels;

namespace ObserwayLabelFlow.App.Controls;

public partial class ToastHost : UserControl
{
    private IToastService? _toastService;

    public ToastHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Attach(ObservableCollection<ToastViewModel> items)
    {
        ToastList.ItemsSource = items;
    }

    public void Detach()
    {
        ToastList.ItemsSource = null;
    }

    internal void PlayEnterAnimation(ToastViewModel toast)
    {
        // Entry animation is handled by the DataTemplate Loaded trigger.
    }

    internal async Task PlayExitAnimationAsync(ToastViewModel toast)
    {
        var card = FindToastCard(toast);
        if (card is null)
        {
            await Task.Delay(120);
            return;
        }

        if (card.RenderTransform is not TranslateTransform slide)
        {
            slide = new TranslateTransform();
            card.RenderTransform = slide;
        }

        var opacity = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var move = new DoubleAnimation(slide.X, slide.X + 24, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        card.BeginAnimation(OpacityProperty, opacity);
        slide.BeginAnimation(TranslateTransform.XProperty, move);

        await Task.Delay(230);
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);

        if (VisualParent is null)
            UnregisterFromService();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
            return;

        _toastService = ((App)Application.Current).Services.GetRequiredService<IToastService>();
        _toastService.RegisterHost(window, this);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnregisterFromService();
    }

    private void UnregisterFromService()
    {
        var window = Window.GetWindow(this);
        if (window is null || _toastService is null)
            return;

        _toastService.UnregisterHost(window);
        _toastService = null;
    }

    private FrameworkElement? FindToastCard(ToastViewModel toast)
    {
        for (var i = 0; i < ToastList.Items.Count; i++)
        {
            if (!Equals(ToastList.Items[i], toast))
                continue;

            if (ToastList.ItemContainerGenerator.ContainerFromIndex(i) is not ContentPresenter presenter)
                break;

            presenter.ApplyTemplate();
            return FindChildByName<Border>(presenter, "ToastCard");
        }

        return null;
    }

    private static T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name)
                return element;

            var nested = FindChildByName<T>(child, name);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
