using System.Globalization;
using ClosedXML.Excel;
using ObserwayLabelFlow.Core.History;

namespace ObserwayLabelFlow.App.Services;

public interface IHistoryExportService
{
    void ExportToExcel(IReadOnlyList<PrintHistoryEntry> entries, string filePath, ILocalizationService localization);

    void ExportInboundToExcel(IReadOnlyList<InboundHistoryEntry> entries, string filePath, ILocalizationService localization);
}

public sealed class HistoryExcelExportService : IHistoryExportService
{
    public void ExportToExcel(IReadOnlyList<PrintHistoryEntry> entries, string filePath, ILocalizationService localization)
    {
        using var workbook = new XLWorkbook();
        WriteHistorySheet(workbook, entries, localization);
        WriteProductsSheet(workbook, entries, localization);
        workbook.SaveAs(filePath);
    }

    public void ExportInboundToExcel(IReadOnlyList<InboundHistoryEntry> entries, string filePath, ILocalizationService localization)
    {
        using var workbook = new XLWorkbook();
        WriteInboundHistorySheet(workbook, entries, localization);
        workbook.SaveAs(filePath);
    }

    private static void WriteInboundHistorySheet(XLWorkbook workbook, IReadOnlyList<InboundHistoryEntry> entries, ILocalizationService localization)
    {
        var sheet = workbook.Worksheets.Add(localization.Get("ModeSelect_ProductInboundTitle"));

        var headers = new[]
        {
            localization.Get("HistoryDateUtc"),
            localization.Get("InboundHistory_Reference"),
            localization.Get("InboundHistory_OrderNumber"),
            localization.Get("InboundHistory_MarkedBy"),
            localization.Get("HistoryResult"),
            localization.Get("HistoryErrorMessage"),
        };

        ApplyHeaderRow(sheet, headers);

        for (var i = 0; i < entries.Count; i++)
        {
            var row = i + 2;
            var entry = entries[i];

            sheet.Cell(row, 1).Value = entry.CreatedAtUtc.ToLocalTime().DateTime;
            sheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            sheet.Cell(row, 2).Value = entry.Reference ?? string.Empty;
            sheet.Cell(row, 3).Value = entry.OrderNumber ?? string.Empty;
            sheet.Cell(row, 4).Value = entry.MarkedBy ?? string.Empty;
            sheet.Cell(row, 5).Value = entry.Success
                ? localization.Get("HistorySuccess")
                : localization.Get("HistoryFailed");
            sheet.Cell(row, 6).Value = entry.ErrorMessage ?? string.Empty;
        }

        sheet.Columns().AdjustToContents(1, headers.Length);
        sheet.SheetView.FreezeRows(1);
    }

    private static void WriteHistorySheet(XLWorkbook workbook, IReadOnlyList<PrintHistoryEntry> entries, ILocalizationService localization)
    {
        var sheet = workbook.Worksheets.Add(localization.Get("TabHistory"));

        var headers = new[]
        {
            localization.Get("HistoryDateUtc"),
            localization.Get("HistoryOrderNumber"),
            localization.Get("AmazonOrderId"),
            localization.Get("HistoryTracking"),
            localization.Get("HistoryCustomer"),
            localization.Get("HistoryCustomerPhone"),
            localization.Get("HistoryCustomerAddress"),
            localization.Get("HistoryCustomerCity"),
            localization.Get("HistoryCustomerState"),
            localization.Get("HistoryCustomerCountry"),
            localization.Get("HistoryCustomerZip"),
            localization.Get("HistoryStatus"),
            localization.Get("HistoryPaymentStatus"),
            localization.Get("HistoryOrderCancelStatus"),
            localization.Get("HistoryOrderSource"),
            localization.Get("HistoryCarrier"),
            localization.Get("HistoryCarrierService"),
            localization.Get("HistoryShippingPrice"),
            localization.Get("HistoryPaymentType"),
            localization.Get("HistoryProductCount"),
            localization.Get("ProductsSectionTitle"),
            localization.Get("HistoryCopies"),
            localization.Get("HistoryPrinter"),
            localization.Get("HistoryPaperSize"),
            localization.Get("HistoryPrintedBy"),
            localization.Get("HistoryResult"),
            localization.Get("HistoryErrorMessage"),
            localization.Get("HistoryNotes"),
            localization.Get("HistoryPdf"),
        };

        ApplyHeaderRow(sheet, headers);

        for (var i = 0; i < entries.Count; i++)
        {
            var row = i + 2;
            var entry = entries[i];
            var snapshot = HistorySnapshotSerializer.TryDeserialize(entry.SnapshotJson);
            var customer = snapshot?.Customer;
            var products = snapshot?.Products ?? [];

            sheet.Cell(row, 1).Value = entry.CreatedAtUtc.UtcDateTime;
            sheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Cell(row, 2).Value = entry.OrderNumber ?? string.Empty;
            sheet.Cell(row, 3).Value = snapshot?.AmazonOrderId ?? string.Empty;
            sheet.Cell(row, 4).Value = entry.TrackingNumber;
            sheet.Cell(row, 5).Value = entry.CustomerName ?? customer?.FullName ?? string.Empty;
            sheet.Cell(row, 6).Value = customer?.PhoneNumber ?? string.Empty;
            sheet.Cell(row, 7).Value = FormatCustomerAddress(customer);
            sheet.Cell(row, 8).Value = customer?.City ?? string.Empty;
            sheet.Cell(row, 9).Value = customer?.State ?? string.Empty;
            sheet.Cell(row, 10).Value = customer?.Country ?? string.Empty;
            sheet.Cell(row, 11).Value = customer?.ZipCode ?? string.Empty;
            sheet.Cell(row, 12).Value = entry.OrderStatus ?? string.Empty;
            sheet.Cell(row, 13).Value = snapshot?.PaymentStatus ?? string.Empty;
            sheet.Cell(row, 14).Value = snapshot?.OrderCancelStatus ?? string.Empty;
            sheet.Cell(row, 15).Value = snapshot?.OrderSource ?? string.Empty;
            sheet.Cell(row, 16).Value = entry.CarrierName ?? string.Empty;
            sheet.Cell(row, 17).Value = snapshot?.CarrierService ?? string.Empty;
            sheet.Cell(row, 18).Value = snapshot?.ShippingPrice ?? string.Empty;
            sheet.Cell(row, 19).Value = snapshot?.PaymentType ?? string.Empty;
            sheet.Cell(row, 20).Value = products.Count > 0 ? products.Count : entry.ProductCount;
            sheet.Cell(row, 21).Value = FormatProductsCombo(products, localization);
            sheet.Cell(row, 21).Style.Alignment.WrapText = true;
            sheet.Cell(row, 22).Value = entry.Copies;
            sheet.Cell(row, 23).Value = entry.PrinterName ?? string.Empty;
            sheet.Cell(row, 24).Value = entry.PaperSize ?? string.Empty;
            sheet.Cell(row, 25).Value = entry.PrintedBy ?? string.Empty;
            sheet.Cell(row, 26).Value = entry.Success
                ? localization.Get("HistorySuccess")
                : localization.Get("HistoryFailed");
            sheet.Cell(row, 27).Value = entry.ErrorMessage ?? string.Empty;
            sheet.Cell(row, 28).Value = entry.Notes ?? string.Empty;
            sheet.Cell(row, 29).Value = entry.PdfUrl ?? string.Empty;
        }

        sheet.Columns().AdjustToContents(1, headers.Length);
        sheet.Column(21).Width = Math.Min(Math.Max(sheet.Column(21).Width, 48), 80);
        sheet.SheetView.FreezeRows(1);
    }

    private static void WriteProductsSheet(XLWorkbook workbook, IReadOnlyList<PrintHistoryEntry> entries, ILocalizationService localization)
    {
        var sheet = workbook.Worksheets.Add(localization.Get("ProductsSectionTitle"));

        var headers = new[]
        {
            localization.Get("HistoryDateUtc"),
            localization.Get("HistoryOrderNumber"),
            localization.Get("HistoryTracking"),
            localization.Get("HistoryCustomer"),
            localization.Get("HistoryProductIndex"),
            localization.Get("ProductOfficialName"),
            localization.Get("ProductAsin"),
            localization.Get("ProductSku"),
            localization.Get("ProductQuantity"),
            localization.Get("ProductLength"),
            localization.Get("ProductWidth"),
            localization.Get("ProductHeight"),
            localization.Get("ProductWeight"),
            localization.Get("ProductSize"),
            localization.Get("ProductImageUrl"),
        };

        ApplyHeaderRow(sheet, headers);

        var row = 2;
        foreach (var entry in entries)
        {
            var snapshot = HistorySnapshotSerializer.TryDeserialize(entry.SnapshotJson);
            var products = snapshot?.Products ?? [];
            if (products.Count == 0)
                continue;

            for (var index = 0; index < products.Count; index++)
            {
                var product = products[index];
                sheet.Cell(row, 1).Value = entry.CreatedAtUtc.UtcDateTime;
                sheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                sheet.Cell(row, 2).Value = entry.OrderNumber ?? string.Empty;
                sheet.Cell(row, 3).Value = entry.TrackingNumber;
                sheet.Cell(row, 4).Value = entry.CustomerName ?? snapshot?.Customer?.FullName ?? string.Empty;
                sheet.Cell(row, 5).Value = index + 1;
                sheet.Cell(row, 6).Value = product.Title?.Trim() ?? string.Empty;
                sheet.Cell(row, 7).Value = product.Asin;
                sheet.Cell(row, 8).Value = product.Sku;
                sheet.Cell(row, 9).Value = product.Quantity;
                sheet.Cell(row, 10).Value = product.Length;
                sheet.Cell(row, 11).Value = product.Width;
                sheet.Cell(row, 12).Value = product.Height;
                sheet.Cell(row, 13).Value = ProductMeasurementFormatter.ToPounds(product.Weight);
                sheet.Cell(row, 14).Value = FormatProductSize(product, localization);
                sheet.Cell(row, 15).Value = product.ImageUrl ?? string.Empty;
                row++;
            }
        }

        sheet.Columns().AdjustToContents(1, headers.Length);
        sheet.SheetView.FreezeRows(1);
    }

    private static void ApplyHeaderRow(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var col = 0; col < headers.Count; col++)
            sheet.Cell(1, col + 1).Value = headers[col];

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#24314F");
        headerRow.Style.Font.FontColor = XLColor.FromHtml("#E9EEF9");
    }

    private static string FormatCustomerAddress(HistoryCustomerSnapshot? customer)
    {
        if (customer is null)
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.AddressLine1))
            parts.Add(customer.AddressLine1.Trim());
        if (!string.IsNullOrWhiteSpace(customer.AddressLine2))
            parts.Add(customer.AddressLine2.Trim());
        if (!string.IsNullOrWhiteSpace(customer.District))
            parts.Add(customer.District.Trim());

        return string.Join(", ", parts);
    }

    private static string FormatProductsCombo(IReadOnlyList<HistoryProductSnapshot> products, ILocalizationService localization)
    {
        if (products.Count == 0)
            return string.Empty;

        var lines = new List<string>(products.Count);
        for (var i = 0; i < products.Count; i++)
        {
            var product = products[i];
            var title = string.IsNullOrWhiteSpace(product.Title) ? "—" : product.Title.Trim();
            lines.Add(string.Join(" | ", new[]
            {
                $"{i + 1}) {title}",
                $"{localization.Get("ProductAsin")}: {product.Asin}",
                $"{localization.Get("ProductSku")}: {product.Sku}",
                $"{localization.Get("ProductQuantity")}: {product.Quantity}",
                $"{localization.Get("ProductSize")}: {FormatProductSize(product, localization)}",
            }));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatProductSize(HistoryProductSnapshot product, ILocalizationService localization)
    {
        var length = product.Length.ToString("0.##", CultureInfo.InvariantCulture);
        var width = product.Width.ToString("0.##", CultureInfo.InvariantCulture);
        var height = product.Height.ToString("0.##", CultureInfo.InvariantCulture);
        var weight = ProductMeasurementFormatter.FormatPounds(product.Weight);
        return localization.Get(
            "ProductSizeFormat",
            length,
            width,
            height,
            weight);
    }
}
