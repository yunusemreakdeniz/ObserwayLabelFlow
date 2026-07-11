using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.Security;

namespace ObserwayLabelFlow.App.Services;

public sealed partial class LabelPdfLoader : ILabelPdfLoader
{
    public const string PreviewFileName = "label-preview.pdf";

    private readonly HttpClient _http;
    private readonly ITokenStore _tokenStore;
    private readonly IApiBaseUrlProvider _apiBaseUrl;
    private readonly ILogger<LabelPdfLoader> _logger;

    public LabelPdfLoader(
        HttpClient http,
        ITokenStore tokenStore,
        IApiBaseUrlProvider apiBaseUrl,
        ILogger<LabelPdfLoader> logger)
    {
        _http = http;
        _tokenStore = tokenStore;
        _apiBaseUrl = apiBaseUrl;
        _logger = logger;

        CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ObserwayLabelFlow",
            "label-cache");
        Directory.CreateDirectory(CacheDirectory);
    }

    public string CacheDirectory { get; }

    public async Task<LabelPdfPreviewRequest> PreparePreviewAsync(Uri source, CancellationToken cancellationToken = default)
    {
        var pdfBytes = await DownloadPdfBytesAsync(source, cancellationToken);
        if (pdfBytes is null || pdfBytes.Length == 0)
            throw new InvalidOperationException($"Etiket PDF indirilemedi: {source}");

        var previewPath = Path.Combine(CacheDirectory, PreviewFileName);
        await File.WriteAllBytesAsync(previewPath, pdfBytes, cancellationToken);

        _logger.LogInformation("Etiket PDF önbelleğe alındı. Boyut={SizeBytes} Url={Url}", pdfBytes.Length, source);

        return new LabelPdfPreviewRequest
        {
            LocalFilePath = previewPath
        };
    }

    private async Task<byte[]?> DownloadPdfBytesAsync(Uri source, CancellationToken cancellationToken)
    {
        if (source.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
            return TryReadDataUriPdf(source);

        var bearerToken = await TryGetBearerTokenAsync(cancellationToken);
        var candidates = BuildDownloadCandidates(source);

        foreach (var candidate in candidates)
        {
            var bytes = await TryDownloadFromUrlAsync(candidate, bearerToken, preferPdfAccept: false, cancellationToken);
            if (bytes is not null)
                return bytes;

            bytes = await TryDownloadFromUrlAsync(candidate, bearerToken, preferPdfAccept: true, cancellationToken);
            if (bytes is not null)
                return bytes;
        }

        return null;
    }

    private async Task<byte[]?> TryDownloadFromUrlAsync(
        Uri url,
        string? bearerToken,
        bool preferPdfAccept,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (preferPdfAccept)
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));

            if (ShouldAttachToken(url, bearerToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Etiket indirme başarısız. Status={StatusCode} Url={Url}",
                    (int)response.StatusCode,
                    url);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (TryExtractPdfBytes(bytes, out var pdfBytes))
                return pdfBytes;

            if (LooksLikeHtml(bytes))
            {
                var html = Encoding.UTF8.GetString(bytes);
                foreach (var pdfUrl in ExtractPdfUrlsFromHtml(html, url))
                {
                    var nested = await TryDownloadFromUrlAsync(pdfUrl, bearerToken, preferPdfAccept: true, cancellationToken);
                    if (nested is not null)
                        return nested;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Etiket indirme denemesi başarısız. Url={Url}", url);
        }

        return null;
    }

    private IEnumerable<Uri> BuildDownloadCandidates(Uri source)
    {
        yield return source;

        if (!source.AbsoluteUri.Contains("/view", StringComparison.OrdinalIgnoreCase))
            yield break;

        var downloadUrl = source.AbsoluteUri
            .Replace("/view", "/download", StringComparison.OrdinalIgnoreCase)
            .Replace("/View", "/download", StringComparison.OrdinalIgnoreCase);

        if (!string.Equals(downloadUrl, source.AbsoluteUri, StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri))
        {
            yield return downloadUri;
        }
    }

    private bool ShouldAttachToken(Uri url, string? bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            return false;

        if (!Uri.TryCreate(_apiBaseUrl.GetBaseUrl(), UriKind.Absolute, out var apiBase))
            return false;

        return string.Equals(url.Host, apiBase.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Uri> ExtractPdfUrlsFromHtml(string html, Uri baseUri)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in PdfSrcAttributeRegex().Matches(html))
        {
            var raw = match.Groups[1].Value;
            if (!TryResolveUrl(raw, baseUri, out var resolved))
                continue;

            if (seen.Add(resolved.AbsoluteUri))
                yield return resolved;
        }

        foreach (Match match in PdfAbsoluteUrlRegex().Matches(html))
        {
            var raw = match.Value;
            if (!TryResolveUrl(raw, baseUri, out var resolved))
                continue;

            if (seen.Add(resolved.AbsoluteUri))
                yield return resolved;
        }
    }

    private static bool TryResolveUrl(string raw, Uri baseUri, out Uri resolved)
    {
        resolved = null!;
        if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute))
        {
            resolved = absolute;
            return true;
        }

        if (Uri.TryCreate(baseUri, raw, out var relative))
        {
            resolved = relative;
            return true;
        }

        return false;
    }

    private static bool TryExtractPdfBytes(byte[] bytes, out byte[] pdfBytes)
    {
        pdfBytes = Array.Empty<byte>();

        if (bytes.Length >= 4
            && bytes[0] == (byte)'%'
            && bytes[1] == (byte)'P'
            && bytes[2] == (byte)'D'
            && bytes[3] == (byte)'F')
        {
            pdfBytes = bytes;
            return true;
        }

        var pdfStart = IndexOfPdfHeader(bytes);
        if (pdfStart >= 0)
        {
            pdfBytes = bytes.AsSpan(pdfStart).ToArray();
            return true;
        }

        return false;
    }

    private static int IndexOfPdfHeader(byte[] bytes)
    {
        for (var i = 0; i <= bytes.Length - 4; i++)
        {
            if (bytes[i] == (byte)'%'
                && bytes[i + 1] == (byte)'P'
                && bytes[i + 2] == (byte)'D'
                && bytes[i + 3] == (byte)'F')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool LooksLikeHtml(byte[] bytes)
    {
        if (bytes.Length == 0)
            return false;

        var sampleLength = Math.Min(bytes.Length, 256);
        var sample = Encoding.UTF8.GetString(bytes, 0, sampleLength).TrimStart();
        return sample.StartsWith("<!", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("<HTML", StringComparison.Ordinal);
    }

    private static byte[]? TryReadDataUriPdf(Uri dataUri)
    {
        var value = dataUri.AbsoluteUri;
        const string prefix = "base64,";
        var index = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        try
        {
            return Convert.FromBase64String(value[(index + prefix.Length)..]);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryGetBearerTokenAsync(CancellationToken cancellationToken)
    {
        var session = await _tokenStore.GetAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(session?.AccessToken)
            ? null
            : session.AccessToken.Trim();
    }

    [GeneratedRegex("""(?:href|src)=["']([^"']+\.pdf[^"']*)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex PdfSrcAttributeRegex();

    [GeneratedRegex("""https?://[^\s"'<>]+\.pdf[^\s"'<>]*""", RegexOptions.IgnoreCase)]
    private static partial Regex PdfAbsoluteUrlRegex();
}
