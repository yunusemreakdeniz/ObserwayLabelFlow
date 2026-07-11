using System.Windows;
using ObserwayLabelFlow.App.Controls;

namespace ObserwayLabelFlow.App.Services;

public enum ToastKind
{
    Success,
    Info,
    Warning,
    Error
}

public interface IToastService
{
    void Show(ToastKind kind, string title, string message, Window? owner = null, TimeSpan? duration = null);

    void RegisterHost(Window window, ToastHost host);

    void UnregisterHost(Window window);
}
