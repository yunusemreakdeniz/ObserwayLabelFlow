using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ObserwayLabelFlow.App.Services;

namespace ObserwayLabelFlow.App.Views;

public partial class AppDialogWindow : Window
{
    public AppDialogWindow(AppDialogKind kind, string title, string message, bool confirm = false)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ApplyKind(kind);

        if (confirm)
        {
            OkButton.Visibility = Visibility.Collapsed;
            ConfirmButtonsPanel.Visibility = Visibility.Visible;
        }

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Enter or Key.Return)
            {
                DialogResult = confirm ? true : true;
                e.Handled = true;
            }
        };
    }

    private void ApplyKind(AppDialogKind kind)
    {
        switch (kind)
        {
            case AppDialogKind.Error:
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x1F, 0x1F));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
                IconBadge.BorderThickness = new Thickness(1);
                IconText.Text = "\uE711";
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
                break;
            case AppDialogKind.Warning:
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x12));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xB9, 0x4C));
                IconBadge.BorderThickness = new Thickness(1);
                IconText.Text = "\uE7BA";
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xB9, 0x4C));
                break;
            default:
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x23, 0x3F));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x6E, 0xA8, 0xFE));
                IconBadge.BorderThickness = new Thickness(1);
                IconText.Text = "\uE946";
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0xA8, 0xFE));
                break;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
