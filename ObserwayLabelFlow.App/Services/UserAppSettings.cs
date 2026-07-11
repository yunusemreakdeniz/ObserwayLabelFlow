namespace ObserwayLabelFlow.App.Services;

public enum BarcodeMode
{
    AutoQueryPrint,
    AutoQueryOnly
}

public enum LabelOrientation
{
    Portrait,
    Landscape
}

public sealed class LabelPrintSettings
{
    public string PrinterName { get; set; } = string.Empty;

    public double PaperWidthMm { get; set; } = 100;

    public double PaperHeightMm { get; set; } = 150;

    public LabelOrientation Orientation { get; set; } = LabelOrientation.Portrait;

    public int Dpi { get; set; } = 300;

    public int Copies { get; set; } = 1;

    public double TopMarginMm { get; set; } = 0;

    public double BottomMarginMm { get; set; } = 0;

    public double LeftMarginMm { get; set; } = 0;

    public double RightMarginMm { get; set; } = 0;
}

public sealed class UserAppSettings
{
    public string UiCulture { get; set; } = string.Empty;

    public string PrinterName { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = string.Empty;

    public int BarcodeTimeoutMs { get; set; } = 250;

    public BarcodeMode BarcodeMode { get; set; } = BarcodeMode.AutoQueryPrint;

    /// <summary>Başarılı barkod okuma sonrası takip alanını temizle.</summary>
    public bool ClearTrackingAfterScan { get; set; } = true;

    /// <summary>Sorgulama sonrası etiketi otomatik yazdır.</summary>
    public bool AutoPrintOnQuery { get; set; }

    public LabelPrintSettings LabelPrintSettings { get; set; } = new();
}
