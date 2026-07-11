using System.Windows;
using System.Windows.Input;
using ObserwayLabelFlow.App.ViewModels;

namespace ObserwayLabelFlow.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.OwnerWindow = this;
        vm.CloseRequested += OnCloseRequested;
        Closed += (_, _) => vm.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested()
    {
        DialogResult = false;
        Close();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
            return;

        SaveButton.IsEnabled = false;
        try
        {
            if (!await vm.TrySaveAsync())
                return;

            DialogResult = true;
            Close();
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
