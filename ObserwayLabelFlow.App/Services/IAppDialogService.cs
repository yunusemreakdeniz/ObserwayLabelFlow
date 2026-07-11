using System.Windows;

namespace ObserwayLabelFlow.App.Services;

public enum AppDialogKind
{
    Info,
    Warning,
    Error
}

public interface IAppDialogService
{
    void Show(AppDialogKind kind, string title, string message, Window? owner = null);

    bool Confirm(string title, string message, Window? owner = null);
}
