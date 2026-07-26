using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ObserwayLabelFlow.App.Views;

public partial class TransferView : UserControl
{
    public TransferView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public event Action? TransferQueryInputChanged;
    public event Action? TransferQuerySubmitRequested;

    public void FocusQueryBox()
    {
        if (!IsVisible)
            return;

        TransferQueryBox.Focus();
        Keyboard.Focus(TransferQueryBox);
        TransferQueryBox.CaretIndex = TransferQueryBox.Text.Length;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            Dispatcher.BeginInvoke(FocusQueryBox, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void TransferQueryBox_TextChanged(object sender, TextChangedEventArgs e)
        => TransferQueryInputChanged?.Invoke();

    private void TransferQueryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Return)
            return;

        e.Handled = true;
        TransferQuerySubmitRequested?.Invoke();
    }
}
