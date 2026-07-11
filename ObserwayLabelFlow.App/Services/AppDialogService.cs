using System.Windows;
using ObserwayLabelFlow.App.Views;

namespace ObserwayLabelFlow.App.Services;

public sealed class AppDialogService : IAppDialogService
{
    private readonly IToastService _toasts;

    public AppDialogService(IToastService toasts)
    {
        _toasts = toasts;
    }

    public void Show(AppDialogKind kind, string title, string message, Window? owner = null)
    {
        var toastKind = kind switch
        {
            AppDialogKind.Info => ToastKind.Success,
            AppDialogKind.Warning => ToastKind.Warning,
            AppDialogKind.Error => ToastKind.Error,
            _ => ToastKind.Info
        };

        _toasts.Show(toastKind, title, message, ResolveOwner(owner));
    }

    public bool Confirm(string title, string message, Window? owner = null)
    {
        var dialog = new AppDialogWindow(AppDialogKind.Warning, title, message, confirm: true);
        var target = ResolveOwner(owner);
        if (target is not null)
        {
            dialog.Owner = target;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true;
    }

    private static Window? ResolveOwner(Window? owner)
    {
        if (owner is { IsLoaded: true })
            return owner;

        var app = Application.Current;
        if (app is null)
            return null;

        return app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsLoaded)
            ?? app.MainWindow;
    }
}
