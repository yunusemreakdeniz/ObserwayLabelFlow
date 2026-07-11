using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ObserwayLabelFlow.App.Services;
using ObserwayLabelFlow.App.ViewModels;

namespace ObserwayLabelFlow.App.Views;

public partial class LoginWindow : Window
{
    private const double ResponsiveBreakpoint = 980;
    private bool _isNarrowLayout;

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

        var narrow = ActualWidth < ResponsiveBreakpoint;
        if (narrow == _isNarrowLayout)
            return;

        _isNarrowLayout = narrow;

        if (narrow)
        {
            LoginContentGrid.ColumnDefinitions.Clear();
            LoginContentGrid.RowDefinitions.Clear();
            LoginContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            LoginContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            LoginContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(BrandingPanel, 0);
            Grid.SetColumn(BrandingPanel, 0);
            Grid.SetColumnSpan(BrandingPanel, 1);

            Grid.SetRow(LoginCard, 2);
            Grid.SetColumn(LoginCard, 0);
            Grid.SetColumnSpan(LoginCard, 1);
            LoginCard.MaxWidth = double.PositiveInfinity;
            LoginCard.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            LoginContentGrid.RowDefinitions.Clear();
            LoginContentGrid.ColumnDefinitions.Clear();
            LoginContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
            LoginContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            LoginContentGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(400),
                MinWidth = 300,
                MaxWidth = 520
            });

            Grid.SetRow(BrandingPanel, 0);
            Grid.SetColumn(BrandingPanel, 0);
            Grid.SetColumnSpan(BrandingPanel, 1);

            Grid.SetRow(LoginCard, 0);
            Grid.SetColumn(LoginCard, 2);
            Grid.SetColumnSpan(LoginCard, 1);
            LoginCard.MaxWidth = 520;
            LoginCard.HorizontalAlignment = HorizontalAlignment.Stretch;
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
