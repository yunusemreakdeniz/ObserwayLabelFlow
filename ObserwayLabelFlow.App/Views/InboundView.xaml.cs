using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ObserwayLabelFlow.App.Infrastructure;
using ObserwayLabelFlow.App.ViewModels;
using ObserwayLabelFlow.Core.History;

namespace ObserwayLabelFlow.App.Views;

public partial class InboundView : UserControl
{
    public InboundView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public event Action? InboundQueryInputChanged;
    public event Action? InboundQuerySubmitRequested;

    public void FocusQueryBox()
    {
        if (!IsVisible)
            return;

        InboundQueryBox.Focus();
        Keyboard.Focus(InboundQueryBox);
        InboundQueryBox.CaretIndex = InboundQueryBox.Text.Length;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            Dispatcher.BeginInvoke(FocusQueryBox, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void InboundQueryBox_TextChanged(object sender, TextChangedEventArgs e)
        => InboundQueryInputChanged?.Invoke();

    private void InboundQueryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Return)
            return;

        e.Handled = true;
        InboundQuerySubmitRequested?.Invoke();
    }

    private void InboundHistoryGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridCell>(e.OriginalSource as DependencyObject) is not DataGridCell cell
            || cell.DataContext is not InboundHistoryEntry entry
            || DataContext is not MainViewModel vm)
        {
            e.Handled = true;
            return;
        }

        InboundHistoryGrid.SelectedItem = entry;
        var cellValue = HistoryGridCellValueResolver.Resolve(cell, entry);
        vm.PrepareInboundHistoryCopy(cellValue);
    }

    private void InboundHistoryContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        menu.DataContext = DataContext;

        if (DataContext is MainViewModel vm)
            vm.CopyHistoryCellCommand.NotifyCanExecuteChanged();
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
                return match;
        }

        return null;
    }
}
