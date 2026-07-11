using System.Windows;
using System.Windows.Controls;

namespace ObserwayLabelFlow.App.Infrastructure;

public static class HistoryGridColumnRole
{
    public static readonly DependencyProperty RoleProperty =
        DependencyProperty.RegisterAttached(
            "Role",
            typeof(string),
            typeof(HistoryGridColumnRole),
            new PropertyMetadata(null));

    public static string? GetRole(DependencyObject obj) => (string?)obj.GetValue(RoleProperty);

    public static void SetRole(DependencyObject obj, string? value) => obj.SetValue(RoleProperty, value);
}
