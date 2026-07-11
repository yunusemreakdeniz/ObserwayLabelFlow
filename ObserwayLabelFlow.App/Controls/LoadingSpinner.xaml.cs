using System.Windows;
using System.Windows.Media;

namespace ObserwayLabelFlow.App.Controls;

public partial class LoadingSpinner
{
    public static readonly DependencyProperty SpinnerBrushProperty =
        DependencyProperty.Register(
            nameof(SpinnerBrush),
            typeof(Brush),
            typeof(LoadingSpinner),
            new PropertyMetadata(Application.Current?.TryFindResource("AccentBrush") as Brush ?? Brushes.DeepSkyBlue));

    public LoadingSpinner()
    {
        InitializeComponent();
    }

    public Brush SpinnerBrush
    {
        get => (Brush)GetValue(SpinnerBrushProperty);
        set => SetValue(SpinnerBrushProperty, value);
    }
}
