using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using ObserwayLabelFlow.Core.History;

namespace ObserwayLabelFlow.App.Infrastructure;

public static class HistoryGridCellValueResolver
{
    public static string? Resolve(DataGridCell cell, PrintHistoryEntry entry)
    {
        var column = cell.Column;
        if (column is null)
            return null;

        if (column is DataGridBoundColumn boundColumn)
            return ResolveBinding(entry, boundColumn.Binding);

        return column.GetValue(HistoryGridColumnRole.RoleProperty) switch
        {
            "Error" => NullIfWhiteSpace(entry.ErrorMessage),
            "Selection" or "Select" or "Print" or "Delete" => null,
            _ => null
        };
    }

    private static string? ResolveBinding(PrintHistoryEntry entry, BindingBase? bindingBase)
    {
        if (bindingBase is not Binding binding || binding.Path.Path is not { Length: > 0 } path)
            return null;

        var value = GetPropertyValue(entry, path);
        if (value is null)
            return null;

        if (!string.IsNullOrEmpty(binding.StringFormat))
            return string.Format(CultureInfo.CurrentCulture, binding.StringFormat, value);

        return Convert.ToString(value, CultureInfo.CurrentCulture);
    }

    private static object? GetPropertyValue(PrintHistoryEntry entry, string path)
        => path switch
        {
            nameof(PrintHistoryEntry.CreatedAtUtc) => entry.CreatedAtUtc,
            nameof(PrintHistoryEntry.OrderNumber) => entry.OrderNumber,
            nameof(PrintHistoryEntry.TrackingNumber) => entry.TrackingNumber,
            nameof(PrintHistoryEntry.CustomerName) => entry.CustomerName,
            nameof(PrintHistoryEntry.OrderStatus) => entry.OrderStatus,
            nameof(PrintHistoryEntry.CarrierName) => entry.CarrierName,
            nameof(PrintHistoryEntry.ProductCount) => entry.ProductCount,
            nameof(PrintHistoryEntry.Copies) => entry.Copies,
            nameof(PrintHistoryEntry.PrinterName) => entry.PrinterName,
            nameof(PrintHistoryEntry.PaperSize) => entry.PaperSize,
            nameof(PrintHistoryEntry.Success) => entry.Success,
            nameof(PrintHistoryEntry.ErrorMessage) => entry.ErrorMessage,
            nameof(PrintHistoryEntry.IsSelected) => entry.IsSelected,
            _ => null
        };

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
