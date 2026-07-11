using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ObserwayLabelFlow.App.Controls;
using ObserwayLabelFlow.App.ViewModels;

namespace ObserwayLabelFlow.App.Services;

public sealed class ToastService : IToastService
{
    private const int MaxVisibleToasts = 5;

    private readonly Dictionary<Window, WindowToastState> _states = new();
    private readonly object _gate = new();

    public void RegisterHost(Window window, ToastHost host)
    {
        if (window is null)
            return;

        window.Dispatcher.Invoke(() =>
        {
            lock (_gate)
            {
                if (!_states.TryGetValue(window, out var state))
                {
                    state = new WindowToastState(window.Dispatcher);
                    _states[window] = state;
                }

                state.Host = host;
                host.Attach(state.Items);
            }
        });
    }

    public void UnregisterHost(Window window)
    {
        if (window is null)
            return;

        window.Dispatcher.Invoke(() =>
        {
            lock (_gate)
            {
                if (!_states.TryGetValue(window, out var state))
                    return;

                state.Host?.Detach();
                state.Host = null;

                if (state.Items.Count == 0)
                    _states.Remove(window);
            }
        });
    }

    public void Show(ToastKind kind, string title, string message, Window? owner = null, TimeSpan? duration = null)
    {
        var target = ResolveWindow(owner);
        if (target is null)
            return;

        var toastDuration = duration ?? GetDefaultDuration(kind);
        var toast = new ToastViewModel(kind, title, message);

        void Enqueue()
        {
            WindowToastState state;
            lock (_gate)
            {
                if (!_states.TryGetValue(target, out state!))
                {
                    state = new WindowToastState(target.Dispatcher);
                    _states[target] = state;
                }
            }

            toast.DismissRequested = t => _ = state.DismissAsync(t);
            state.Enqueue(toast, toastDuration, MaxVisibleToasts);
        }

        if (target.Dispatcher.CheckAccess())
            Enqueue();
        else
            target.Dispatcher.BeginInvoke(Enqueue, DispatcherPriority.Normal);
    }

    private static Window? ResolveWindow(Window? owner)
    {
        if (owner is { IsLoaded: true })
            return owner;

        var app = Application.Current;
        if (app is null)
            return null;

        return app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsLoaded)
            ?? app.Windows.OfType<Window>().FirstOrDefault(w => w.IsLoaded)
            ?? app.MainWindow;
    }

    private static TimeSpan GetDefaultDuration(ToastKind kind) => kind switch
    {
        ToastKind.Error => TimeSpan.FromSeconds(7),
        ToastKind.Warning => TimeSpan.FromSeconds(5.5),
        _ => TimeSpan.FromSeconds(4)
    };

    private sealed class WindowToastState
    {
        private readonly Dictionary<Guid, DispatcherTimer> _timers = new();

        public WindowToastState(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        public Dispatcher Dispatcher { get; }

        public ObservableCollection<ToastViewModel> Items { get; } = new();

        public ToastHost? Host { get; set; }

        public void Enqueue(ToastViewModel toast, TimeSpan duration, int maxVisible)
        {
            while (Items.Count >= maxVisible)
                _ = DismissAsync(Items[^1], animate: false);

            Items.Insert(0, toast);
            Host?.PlayEnterAnimation(toast);

            var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = duration
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _timers.Remove(toast.Id);
                _ = DismissAsync(toast);
            };
            _timers[toast.Id] = timer;
            timer.Start();
        }

        public async Task DismissAsync(ToastViewModel toast, bool animate = true)
        {
            if (toast.IsClosing)
                return;

            toast.MarkClosing();

            if (_timers.Remove(toast.Id, out var timer))
                timer.Stop();

            if (animate && Host is not null)
                await Host.PlayExitAnimationAsync(toast);

            Items.Remove(toast);
        }
    }
}
