using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ObserwayLabelFlow.App.Services;

namespace ObserwayLabelFlow.App.Infrastructure;

public static class FieldLabelAssist
{
    public static readonly DependencyProperty LocKeyProperty =
        DependencyProperty.RegisterAttached(
            "LocKey",
            typeof(string),
            typeof(FieldLabelAssist),
            new PropertyMetadata(null, OnLocKeyChanged));

    public static readonly DependencyProperty AppendColonProperty =
        DependencyProperty.RegisterAttached(
            "AppendColon",
            typeof(bool),
            typeof(FieldLabelAssist),
            new PropertyMetadata(false, OnAppendColonChanged));

    private static readonly ConditionalWeakTable<TextBlock, string> TrackedBlocks = new();
    private static bool _cultureHooked;

    public static string? GetLocKey(DependencyObject obj) => (string?)obj.GetValue(LocKeyProperty);

    public static void SetLocKey(DependencyObject obj, string? value) => obj.SetValue(LocKeyProperty, value);

    public static bool GetAppendColon(DependencyObject obj) => (bool)obj.GetValue(AppendColonProperty);

    public static void SetAppendColon(DependencyObject obj, bool value) => obj.SetValue(AppendColonProperty, value);

    public static void EnsureCultureHook(ILocalizationService localization)
    {
        if (_cultureHooked)
            return;

        localization.CultureChanged += (_, _) => RefreshAll();
        _cultureHooked = true;
    }

    private static void OnLocKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock)
            Attach(textBlock);
    }

    private static void OnAppendColonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock && (bool)e.NewValue)
            Attach(textBlock);
    }

    private static void Attach(TextBlock textBlock)
    {
        textBlock.Loaded -= OnLoaded;
        textBlock.Loaded += OnLoaded;
        textBlock.Unloaded -= OnUnloaded;
        textBlock.Unloaded += OnUnloaded;

        if (textBlock.IsLoaded)
            Register(textBlock);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock)
            Register(textBlock);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock)
            TrackedBlocks.Remove(textBlock);
    }

    private static void Register(TextBlock textBlock)
    {
        var key = ResolveKey(textBlock);
        if (string.IsNullOrWhiteSpace(key))
            return;

        TrackedBlocks.Remove(textBlock);
        TrackedBlocks.Add(textBlock, key);
        UpdateText(textBlock, key);
    }

    private static string? ResolveKey(TextBlock textBlock)
    {
        var explicitKey = GetLocKey(textBlock);
        if (!string.IsNullOrWhiteSpace(explicitKey))
            return explicitKey.Trim();

        return null;
    }

    private static void RefreshAll()
    {
        foreach (var pair in TrackedBlocks)
        {
            if (pair.Key.IsLoaded)
                UpdateText(pair.Key, pair.Value);
        }
    }

    private static void UpdateText(TextBlock textBlock, string key)
    {
        var resourceKey = key.StartsWith("Loc_", StringComparison.Ordinal) ? key : $"Loc_{key}";
        var text = Application.Current?.TryFindResource(resourceKey) as string;
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (GetAppendColon(textBlock) && !text.EndsWith(':'))
            text += ":";

        textBlock.Text = text;
    }
}
