using System.Globalization;

using ObserwayLabelFlow.App.ViewModels;

using ObserwayLabelFlow.Core.History;

using ObserwayLabelFlow.Core.Orders;



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



    public static ProductPreviewItem ToProductPreviewItem(OrderProductDto product)

        => new()

        {

            OfficialName = string.IsNullOrWhiteSpace(product.Title) ? null : product.Title.Trim(),

            Asin = product.Asin,

            Sku = product.Sku,

            Quantity = product.Quantity.ToString(CultureInfo.InvariantCulture),

            Size = FormatProductSize(product),

            ImageUrl = product.ResolveImageUrl(),

        };



    public static PrintHistoryEntry CreateHistoryEntry(

        OrderDto order,

        string trackingNumber,

        LabelPrintSettings labelSettings,

        string? printedBy,

        string notes)

        => new()

        {

            TrackingNumber = trackingNumber,

            CreatedAtUtc = DateTimeOffset.UtcNow,

            PdfUrl = order.Label ?? string.Empty,

            Notes = notes,

            OrderNumber = order.ObserwayOrderNumber,

            CustomerName = order.Customer?.FullName,

            OrderStatus = order.OrderStatus,

            CarrierName = order.CarrierName,

            ProductCount = order.Products.Count,

            PrinterName = labelSettings.PrinterName,

            Copies = labelSettings.Copies,

            Success = false,

            PaperSize = $"{labelSettings.PaperWidthMm:F1} x {labelSettings.PaperHeightMm:F1} mm",

            PrintedBy = printedBy,

            SnapshotJson = HistorySnapshotSerializer.Serialize(HistorySnapshotSerializer.FromOrder(order)),

        };

}


