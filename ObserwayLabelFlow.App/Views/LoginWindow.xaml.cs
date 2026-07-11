using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ObserwayLabelFlow.App.ViewModels;

namespace ObserwayLabelFlow.App.Views;

public partial class LoginWindow : Window
{
    private const double NarrowWidthBreakpoint = 820;
    private const double CompactHeightBreakpoint = 600;

    private bool _isNarrowLayout;
    private bool _isCompactLayout;

    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.LoginSucceeded += OnLoginSucceeded;
        Closed += (_, _) => vm.LoginSucceeded -= OnLoginSucceeded;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => ApplyResponsiveLayout();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        if (DataContext is LoginViewModel vm)
            await vm.InitializeAsync();
    }

    private void ApplyResponsiveLayout()
    {
        if (LoginContentGrid is null || BrandingPanel is null || LoginCard is null)
            return;

        var narrow = ActualWidth < NarrowWidthBreakpoint;
        var compact = ActualHeight < CompactHeightBreakpoint || narrow;
        if (narrow == _isNarrowLayout && compact == _isCompactLayout)
            return;

        _isNarrowLayout = narrow;
        _isCompactLayout = compact;

        ApplyLayoutStructure(narrow);
        ApplyCompactMetrics(compact, narrow);
    }

    private void ApplyLayoutStructure(bool narrow)
    {
        LoginContentGrid.ColumnDefinitions.Clear();
        LoginContentGrid.RowDefinitions.Clear();

        if (narrow)
        {
            LoginContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            LoginContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(BrandingPanel, 0);
            Grid.SetColumn(BrandingPanel, 0);
            Grid.SetColumnSpan(BrandingPanel, 1);

            Grid.SetRow(LoginCard, 1);
            Grid.SetColumn(LoginCard, 0);
            Grid.SetColumnSpan(LoginCard, 1);
            LoginCard.MaxWidth = double.PositiveInfinity;
            LoginCard.HorizontalAlignment = HorizontalAlignment.Stretch;
            LoginCard.VerticalAlignment = VerticalAlignment.Center;
            LoginCard.Margin = new Thickness(0);
            BrandingPanel.VerticalAlignment = VerticalAlignment.Top;
        }
        else
        {
            LoginContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            LoginContentGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 160
            });
            LoginContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            LoginContentGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(360),
                MinWidth = 280,
                MaxWidth = 420
            });

            Grid.SetRow(BrandingPanel, 0);
            Grid.SetColumn(BrandingPanel, 0);
            Grid.SetColumnSpan(BrandingPanel, 1);

            Grid.SetRow(LoginCard, 0);
            Grid.SetColumn(LoginCard, 2);
            Grid.SetColumnSpan(LoginCard, 1);
            LoginCard.MaxWidth = 420;
            LoginCard.HorizontalAlignment = HorizontalAlignment.Stretch;
            LoginCard.VerticalAlignment = VerticalAlignment.Center;
            LoginCard.Margin = new Thickness(0);
            BrandingPanel.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    private void ApplyCompactMetrics(bool compact, bool narrow)
    {
        LoginContentGrid.Margin = compact
            ? new Thickness(16, 10, 16, 10)
            : new Thickness(24, 16, 24, 16);

        LoginCard.Padding = compact ? new Thickness(16, 14, 16, 14) : new Thickness(20, 18, 20, 18);

        if (LoginFormGrid is not null && LoginFormGrid.RowDefinitions.Count >= 9)
        {
            LoginFormGrid.RowDefinitions[1].Height = new GridLength(compact ? 12 : 24);
            LoginFormGrid.RowDefinitions[3].Height = new GridLength(compact ? 8 : 14);
            LoginFormGrid.RowDefinitions[5].Height = new GridLength(compact ? 10 : 18);
            LoginFormGrid.RowDefinitions[7].Height = new GridLength(compact ? 8 : 16);
        }

        var fieldHeight = compact ? 44.0 : 50.0;
        var buttonHeight = compact ? 46.0 : 52.0;

        if (UsernameBox is not null)
            UsernameBox.Height = fieldHeight;
        if (PasswordBox is not null)
            PasswordBox.Height = fieldHeight;
        if (ClearSessionButton is not null)
            ClearSessionButton.Height = buttonHeight;
        if (LoginButton is not null)
            LoginButton.Height = buttonHeight;

        if (LoginTitleText is not null)
            LoginTitleText.FontSize = compact ? 20 : 22;
        if (LoginSubtitleText is not null)
            LoginSubtitleText.Margin = new Thickness(0, compact ? 4 : 6, 0, 0);

        if (RememberMeCheckBox is not null)
            RememberMeCheckBox.Margin = new Thickness(0, compact ? 8 : 12, 0, 0);

        ApplyBrandingLayout(compact, narrow);
    }

    private void ApplyBrandingLayout(bool compact, bool narrow)
    {
        if (BrandingStack is null || HeroLogoImage is null || BrandTitleText is null || TaglineText is null)
            return;

        BrandingPanel.Margin = new Thickness(0, compact ? 0 : 4, 0, 0);
        BrandingStack.VerticalAlignment = narrow && compact
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;

        if (narrow)
        {
            BrandingStack.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
            HeroLogoImage.MaxWidth = compact ? 96 : 180;
            HeroLogoImage.MaxHeight = compact ? 44 : 120;
            BrandTitleText.FontSize = compact ? 18 : 20;
            BrandTitleText.Margin = compact
                ? new Thickness(10, 0, 0, 0)
                : new Thickness(0, 8, 0, 0);
            BrandTitleText.VerticalAlignment = compact
                ? VerticalAlignment.Center
                : VerticalAlignment.Top;
            TaglineText.Visibility = Visibility.Collapsed;
            TaglineText.Margin = new Thickness(0);
            TaglineText.FontSize = compact ? 13 : 15;
        }
        else
        {
            BrandingStack.Orientation = Orientation.Vertical;
            HeroLogoImage.MaxWidth = compact ? 320 : 440;
            HeroLogoImage.ClearValue(FrameworkElement.MaxHeightProperty);
            BrandTitleText.FontSize = compact ? 20 : 22;
            BrandTitleText.Margin = new Thickness(0, compact ? 8 : 10, 0, 0);
            BrandTitleText.ClearValue(FrameworkElement.VerticalAlignmentProperty);
            TaglineText.Visibility = Visibility.Visible;
            TaglineText.Margin = new Thickness(0, compact ? 12 : 16, 0, 0);
            TaglineText.FontSize = compact ? 14 : 15;
        }
    }

    private void OnLoginSucceeded()
    {
        Dispatcher.Invoke(() =>
        {
            var main = ((App)Application.Current).Services.GetRequiredService<MainWindow>();
            main.Show();
            Close();
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            Window.GetWindow(fe)?.Close();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            var w = Window.GetWindow(fe);
            if (w is not null)
                w.WindowState = WindowState.Minimized;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d)
        {
            if (FindAncestor<ComboBox>(d) is not null)
                return;
            if (FindAncestor<Button>(d) is not null)
                return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var cur = start;
        while (cur is not null)
        {
            if (cur is T typed)
                return typed;
            cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
        }

        return null;
    }

    private void PasswordBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Return)
            return;

        if (DataContext is not LoginViewModel vm || !vm.LoginCommand.CanExecute(null))
            return;

        e.Handled = true;
        vm.LoginCommand.Execute(null);
    }
}
