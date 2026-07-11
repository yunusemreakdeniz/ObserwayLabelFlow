using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObserwayLabelFlow.App.Services;

namespace ObserwayLabelFlow.App.ViewModels;

public sealed partial class ToastViewModel : ObservableObject
{
    public ToastViewModel(ToastKind kind, string title, string message)
    {
        Kind = kind;
        Title = title;
        Message = message;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public ToastKind Kind { get; }

    public string Title { get; }

    public string Message { get; }

    public bool IsClosing { get; private set; }

    public Action<ToastViewModel>? DismissRequested { get; set; }

    [RelayCommand]
    private void Dismiss() => DismissRequested?.Invoke(this);

    internal void MarkClosing() => IsClosing = true;
}
