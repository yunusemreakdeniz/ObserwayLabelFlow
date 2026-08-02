using System.Globalization;
using ObserwayLabelFlow.App.ViewModels;
using ObserwayLabelFlow.Core.History;
using ObserwayLabelFlow.Core.Orders;
using ObserwayLabelFlow.Core.Warehouse;

namespace ObserwayLabelFlow.App.Services;

internal static class OrderPresentationMapper
{
    public static Uri? TryCreateLabelUri(string? labelUrl, string? apiBaseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(labelUrl))
            return null;

        var trimmed = labelUrl.Trim();

        if (trimmed.StartsWith("data:application/pdf", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(trimmed, UriKind.Absolute, out var dataUri))
        {
            return dataUri;
        }

        if (LooksLikeBase64Pdf(trimmed)
            && Uri.TryCreate($"data:application/pdf;base64,{trimmed}", UriKind.Absolute, out var base64Uri))
        {
            return base64Uri;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
            return absolute;

        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            var combined = $"{apiBaseUrl.Trim().TrimEnd('/')}/{trimmed.TrimStart('/')}";
            if (Uri.TryCreate(combined, UriKind.Absolute, out var relative))
                return relative;
        }

        return null;
    }

    private static bool LooksLikeBase64Pdf(string value)
    {
        if (value.Length < 32 || value.Contains(' ') || value.Contains('\n'))
            return false;

        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length >= 4
                && bytes[0] == (byte)'%'
                && bytes[1] == (byte)'P'
                && bytes[2] == (byte)'D'
                && bytes[3] == (byte)'F';
        }
        catch
        {
            return false;
        }
    }

    public static string FormatProductSize(OrderProductDto product)
    {
        var length = product.Length.ToString("0.##", CultureInfo.InvariantCulture);
        var width = product.Width.ToString("0.##", CultureInfo.InvariantCulture);
        var height = product.Height.ToString("0.##", CultureInfo.InvariantCulture);
        var weight = ProductMeasurementFormatter.FormatPounds(product.Weight);
        return $"{length} x {width} x {height} cm / {weight} lbs";
    }

    public static ProductPreviewItem ToProductPreviewItem(OrderProductDto product, string? apiBaseUrl = null)
    {
        var imageUrl = product.ResolveImageUrl();
        if (!string.IsNullOrWhiteSpace(imageUrl))
            imageUrl = ResolveAbsoluteUrl(imageUrl, apiBaseUrl);

        return new ProductPreviewItem
        {
            OfficialName = product.ResolveTitle(),
            Asin = product.Asin,
            Sku = product.Sku,
            Quantity = product.Quantity.ToString(CultureInfo.InvariantCulture),
            Size = FormatProductSize(product),
            ImageUrl = imageUrl,
        };
    }

    public static ProductPreviewItem ToProductPreviewItem(WarehouseProductDto product, string? apiBaseUrl = null)
    {
        var imageUrl = product.ImageUrl;
        if (!string.IsNullOrWhiteSpace(imageUrl))
            imageUrl = ResolveAbsoluteUrl(imageUrl, apiBaseUrl);

        return new()
        {
            OfficialName = string.IsNullOrWhiteSpace(product.Title) ? product.Asin : product.Title.Trim(),
            Asin = product.Asin,
            Sku = product.Sku ?? string.Empty,
            Quantity = product.Quantity.ToString(CultureInfo.InvariantCulture),
            Size = FormatWarehouseProductSize(product),
            ImageUrl = imageUrl,
        };
    }

    private static string FormatWarehouseProductSize(WarehouseProductDto product)
    {
        if (product.Length is null && product.Width is null && product.Height is null && product.Weight is null)
            return string.Empty;

        var length = (product.Length ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        var width = (product.Width ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        var height = (product.Height ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        var weight = product.Weight is null
            ? string.Empty
            : ProductMeasurementFormatter.FormatPounds(product.Weight.Value);
        if (string.IsNullOrEmpty(weight))
            return $"{length} x {width} x {height} cm";
        return $"{length} x {width} x {height} cm / {weight} lbs";
    }

    private static string? ResolveAbsoluteUrl(string url, string? apiBaseUrl)
    {
        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
            return trimmed;

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            return trimmed;

        var combined = $"{apiBaseUrl.Trim().TrimEnd('/')}/{trimmed.TrimStart('/')}";
        return Uri.TryCreate(combined, UriKind.Absolute, out _) ? combined : trimmed;
    }

    public static PrintHistoryEntry CreateHistoryEntry(
        WarehouseLookupDto order,
        string trackingNumber,
        LabelPrintSettings labelSettings,
        string? printedBy,
        string notes,
        ILocalizationService localization)
        => new()
        {
            TrackingNumber = trackingNumber,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PdfUrl = order.LabelUrl ?? string.Empty,
            Notes = notes,
            OrderNumber = order.OrderNumber ?? string.Empty,
            CustomerName = null,
            OrderStatus = OrderStatusLocalizer.GetDisplay(localization, order),
            CarrierName = order.CarrierName,
            ProductCount = order.Products?.Count ?? 0,
            PrinterName = labelSettings.PrinterName,
            Copies = labelSettings.Copies,
            Success = false,
            PaperSize = $"{labelSettings.PaperWidthMm:F1} x {labelSettings.PaperHeightMm:F1} mm",
            PrintedBy = printedBy,
            SnapshotJson = HistorySnapshotSerializer.Serialize(HistorySnapshotSerializer.FromWarehouseLookup(order, localization)),
        };

    public static string FormatCarrierDisplay(string? carrierName, string? carrierService)
    {
        var name = carrierName?.Trim();
        var service = carrierService?.Trim();

        if (string.IsNullOrEmpty(name))
            return service ?? string.Empty;

        if (string.IsNullOrEmpty(service))
            return name;

        if (name.Contains(service, StringComparison.OrdinalIgnoreCase))
            return name;

        return $"{name} - {service}";
    }
}
