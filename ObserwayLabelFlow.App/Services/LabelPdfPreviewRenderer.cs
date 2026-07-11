using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfiumViewer;

namespace ObserwayLabelFlow.App.Services;

public static class LabelPdfPreviewRenderer
{
    public static ImageSource? RenderFirstPage(string pdfPath, double dpi = 144)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return null;

        using var document = PdfDocument.Load(pdfPath);
        if (document.PageCount == 0)
            return null;

        var width = (int)dpi;
        var height = (int)dpi;
        using var image = document.Render(0, width, height, PdfRenderFlags.ForPrinting);
        using var bitmap = new Bitmap(image);
        return ConvertToImageSource(bitmap);
    }

    private static ImageSource ConvertToImageSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
