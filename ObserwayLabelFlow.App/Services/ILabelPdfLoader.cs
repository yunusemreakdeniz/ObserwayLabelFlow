namespace ObserwayLabelFlow.App.Services;

public sealed class LabelPdfPreviewRequest
{
    public required string LocalFilePath { get; init; }
}

public interface ILabelPdfLoader
{
    string CacheDirectory { get; }

    Task<LabelPdfPreviewRequest> PreparePreviewAsync(Uri source, CancellationToken cancellationToken = default);
}

public interface ILabelPrintService
{
    Task<bool> PrintAsync(string pdfPath, LabelPrintSettings settings, CancellationToken cancellationToken = default);
}
