using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using ObserwayLabelFlow.Core.History;

namespace ObserwayLabelFlow.App.Infrastructure;

public static class HistoryGridCellValueResolver
{
    public static string? Resolve(DataGridCell cell, PrintHistoryEntry entry)
        => ResolveCore(cell, path => GetPrintPropertyValue(entry, path));

    public static string? Resolve(DataGridCell cell, InboundHistoryEntry entry)
        => ResolveCore(cell, path => GetInboundPropertyValue(entry, path));

    private static string? ResolveCore(DataGridCell cell, Func<string, object?> getProperty)
    {
        var column = cell.Column;
        if (column is null)
            return null;

        if (column is DataGridBoundColumn boundColumn)
            return ResolveBinding(getProperty, boundColumn.Binding);

        return column.GetValue(HistoryGridColumnRole.RoleProperty) switch
        {
            "Error" => null,
            "Selection" or "Select" or "Print" or "Delete" => null,
            _ => null
        };
    }

    private static string? ResolveBinding(Func<string, object?> getProperty, BindingBase? bindingBase)
    {
        if (bindingBase is not Binding binding || binding.Path.Path is not { Length: > 0 } path)
            return null;

        var value = getProperty(path);
        if (value is null)
            return null;

        if (!string.IsNullOrEmpty(binding.StringFormat))
            return string.Format(CultureInfo.CurrentCulture, binding.StringFormat, value);

        return Convert.ToString(value, CultureInfo.CurrentCulture);
    }

    private static object? GetPrintPropertyValue(PrintHistoryEntry entry, string path)
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

    private static object? GetInboundPropertyValue(InboundHistoryEntry entry, string path)
        => path switch
        {
            nameof(InboundHistoryEntry.CreatedAtUtc) => entry.CreatedAtUtc,
            nameof(InboundHistoryEntry.Reference) => entry.Reference,
            nameof(InboundHistoryEntry.OrderNumber) => entry.OrderNumber,
            nameof(InboundHistoryEntry.MarkedBy) => entry.MarkedBy,
            nameof(InboundHistoryEntry.Success) => entry.Success,
            nameof(InboundHistoryEntry.ErrorMessage) => entry.ErrorMessage,
            _ => null
        };
}
