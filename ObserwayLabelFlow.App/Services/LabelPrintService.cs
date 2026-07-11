using System.Drawing.Printing;
using System.IO;
using Microsoft.Extensions.Logging;
using PdfiumViewer;

namespace ObserwayLabelFlow.App.Services;

public sealed class LabelPrintService(ILogger<LabelPrintService> logger) : ILabelPrintService
{
    public Task<bool> PrintAsync(string pdfPath, LabelPrintSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            throw new FileNotFoundException("Etiket PDF dosyası bulunamadı.", pdfPath);

        return Task.Run(() => PrintInternal(pdfPath, settings), cancellationToken);
    }

    private bool PrintInternal(string pdfPath, LabelPrintSettings settings)
    {
        using var document = PdfDocument.Load(pdfPath);
        using var printDocument = document.CreatePrintDocument();
        printDocument.PrintController = new StandardPrintController();

        if (!string.IsNullOrWhiteSpace(settings.PrinterName))
            printDocument.PrinterSettings.PrinterName = settings.PrinterName;

        printDocument.PrinterSettings.Copies = (short)Math.Clamp(settings.Copies, 1, 99);

        var widthMm = settings.PaperWidthMm;
        var heightMm = settings.PaperHeightMm;
        if (settings.Orientation == LabelOrientation.Landscape)
            (widthMm, heightMm) = (heightMm, widthMm);

        var widthHundredths = (int)Math.Round(widthMm / 25.4 * 100);
        var heightHundredths = (int)Math.Round(heightMm / 25.4 * 100);
        if (widthHundredths > 0 && heightHundredths > 0)
        {
            printDocument.DefaultPageSettings.PaperSize = new PaperSize("LabelFlow", widthHundredths, heightHundredths);
            printDocument.DefaultPageSettings.Landscape = settings.Orientation == LabelOrientation.Landscape;
        }

        logger.LogInformation(
            "Etiket yazdırılıyor. Printer={Printer} Copies={Copies} File={File}",
            printDocument.PrinterSettings.PrinterName,
            printDocument.PrinterSettings.Copies,
            pdfPath);

        printDocument.Print();
        return true;
    }
}
