using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.Core.Common;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.Security;
using ObserwayLabelFlow.Core.Warehouse;
using ObserwayLabelFlow.Infrastructure.Http;

namespace ObserwayLabelFlow.Infrastructure.Warehouse;

public sealed class WarehouseApiClient(
    HttpClient http,
    ITokenStore tokenStore,
    IApiBaseUrlProvider apiBaseUrl,
    ILogger<WarehouseApiClient> logger) : IWarehouseApiClient
{
    public async Task<Result<IReadOnlyList<WarehouseDto>>> GetMyWarehousesAsync(CancellationToken ct = default)
    {
        var auth = await TryAuthorizeAsync(ct);
        if (auth is null)
            return Result<IReadOnlyList<WarehouseDto>>.Fail("Oturum bilgisi bulunamadı.");

        var url = $"{Base()}/api/v1/warehouse/my-warehouses";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);

        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var payload = await resp.Content.ReadFromJsonAsync<List<WarehouseDto>>(HttpJson.DefaultOptions, ct);
            payload ??= [];
            logger.LogInformation("My warehouses loaded. Count={Count}", payload.Count);
            return Result<IReadOnlyList<WarehouseDto>>.Success(payload);
        }

        return await FailAsync<IReadOnlyList<WarehouseDto>>(resp, "Depo listesi alınamadı.", ct);
    }

    public async Task<Result<WarehouseLookupDto>> LookupAsync(
        string reference,
        long warehouseId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return Result<WarehouseLookupDto>.Fail("Referans kodu zorunludur (OBS sipariş numarası veya satın alma takip numarası).");
        if (warehouseId <= 0)
            return Result<WarehouseLookupDto>.Fail("Depo seçilmedi.");

        var auth = await TryAuthorizeAsync(ct);
        if (auth is null)
            return Result<WarehouseLookupDto>.Fail("Oturum bilgisi bulunamadı.");

        var url =
            $"{Base()}/api/v1/warehouse/lookup?reference={Uri.EscapeDataString(reference.Trim())}&warehouseId={warehouseId}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);

        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var payload = await resp.Content.ReadFromJsonAsync<WarehouseLookupDto>(HttpJson.DefaultOptions, ct);
            if (payload is null)
                return Result<WarehouseLookupDto>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            payload.Products ??= new List<WarehouseProductDto>();
            logger.LogInformation(
                "Warehouse lookup. Reference={Reference} WarehouseId={WarehouseId} Matched={Matched} Order={Order}",
                reference,
                warehouseId,
                payload.Matched,
                payload.OrderNumber);

            return Result<WarehouseLookupDto>.Success(payload);
        }

        return await FailAsync<WarehouseLookupDto>(resp, "Sorgu başarısız.", ct);
    }

    public async Task<Result<WarehouseInboundResult>> PostInboundAsync(
        WarehouseInboundRequest request,
        CancellationToken ct = default)
    {
        if (request.WarehouseId <= 0)
            return Result<WarehouseInboundResult>.Fail("Depo seçilmedi.");
        if (string.IsNullOrWhiteSpace(request.Reference))
            return Result<WarehouseInboundResult>.Fail("Referans kodu zorunludur.");

        var auth = await TryAuthorizeAsync(ct);
        if (auth is null)
            return Result<WarehouseInboundResult>.Fail("Oturum bilgisi bulunamadı.");

        var url = $"{Base()}/api/v1/warehouse/inbound";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        req.Content = JsonContent.Create(new InboundBody
        {
            WarehouseId = request.WarehouseId,
            Reference = request.Reference.Trim(),
            OrderId = request.OrderId,
            InboundTrackingNumber = request.InboundTrackingNumber,
            InboundCarrier = request.InboundCarrier,
            Note = request.Note
        }, options: HttpJson.DefaultOptions);

        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var payload = await resp.Content.ReadFromJsonAsync<InboundResponse>(HttpJson.DefaultOptions, ct);
            if (payload is null)
                return Result<WarehouseInboundResult>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            return Result<WarehouseInboundResult>.Success(new WarehouseInboundResult(
                payload.OperationId,
                payload.WarehouseId,
                payload.ScannedReference?.Trim() ?? request.Reference.Trim(),
                payload.Matched,
                payload.OrderId,
                payload.OrderNumber?.Trim(),
                payload.MatchStatus,
                payload.InboundReceivedAt));
        }

        return await FailAsync<WarehouseInboundResult>(resp, "Depo girişi başarısız.", ct);
    }

    public async Task<Result<WarehouseOutboundResult>> PostOutboundAsync(
        WarehouseOutboundRequest request,
        CancellationToken ct = default)
    {
        if (request.WarehouseId <= 0)
            return Result<WarehouseOutboundResult>.Fail("Depo seçilmedi.");
        if (string.IsNullOrWhiteSpace(request.Reference))
            return Result<WarehouseOutboundResult>.Fail("Referans kodu zorunludur.");

        var auth = await TryAuthorizeAsync(ct);
        if (auth is null)
            return Result<WarehouseOutboundResult>.Fail("Oturum bilgisi bulunamadı.");

        var url = $"{Base()}/api/v1/warehouse/outbound";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        req.Content = JsonContent.Create(new OutboundBody
        {
            WarehouseId = request.WarehouseId,
            Reference = request.Reference.Trim(),
            OrderId = request.OrderId,
            Note = request.Note
        }, options: HttpJson.DefaultOptions);

        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var payload = await resp.Content.ReadFromJsonAsync<OutboundResponse>(HttpJson.DefaultOptions, ct);
            if (payload is null)
                return Result<WarehouseOutboundResult>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            return Result<WarehouseOutboundResult>.Success(new WarehouseOutboundResult(
                payload.OperationId,
                payload.WarehouseId,
                payload.ScannedReference?.Trim() ?? request.Reference.Trim(),
                payload.Matched,
                payload.OrderId,
                payload.OrderNumber?.Trim(),
                payload.MatchStatus,
                payload.LabelUrl,
                payload.OutboundReadyAt));
        }

        return await FailAsync<WarehouseOutboundResult>(resp, "Çıkış işaretleme başarısız.", ct);
    }

    private string Base() => apiBaseUrl.GetBaseUrl().TrimEnd('/');

    private async Task<string?> TryAuthorizeAsync(CancellationToken ct)
    {
        var session = await tokenStore.GetAsync(ct);
        return string.IsNullOrWhiteSpace(session?.AccessToken) ? null : session!.AccessToken.Trim();
    }

    private static async Task<Result<T>> FailAsync<T>(HttpResponseMessage resp, string fallbackPrefix, CancellationToken ct)
    {
        return resp.StatusCode switch
        {
            HttpStatusCode.BadRequest => Result<T>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Geçersiz istek.", ct)),
            HttpStatusCode.Unauthorized => Result<T>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Oturum süresi doldu veya geçersiz token. Lütfen tekrar giriş yapın.", ct)),
            HttpStatusCode.Forbidden => Result<T>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Bu işlem için yetkiniz yok.", ct)),
            HttpStatusCode.NotFound => Result<T>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Kayıt bulunamadı.", ct)),
            _ => Result<T>.Fail($"{fallbackPrefix} (HTTP {(int)resp.StatusCode} {resp.ReasonPhrase})"),
        };
    }

    private sealed class InboundBody
    {
        [JsonPropertyName("warehouseId")]
        public long WarehouseId { get; set; }

        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public long? OrderId { get; set; }

        [JsonPropertyName("inboundTrackingNumber")]
        public string? InboundTrackingNumber { get; set; }

        [JsonPropertyName("inboundCarrier")]
        public string? InboundCarrier { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class InboundResponse
    {
        public long OperationId { get; set; }
        public long WarehouseId { get; set; }
        public string? ScannedReference { get; set; }
        public bool Matched { get; set; }
        public long? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? MatchStatus { get; set; }
        public DateTimeOffset? InboundReceivedAt { get; set; }
    }

    private sealed class OutboundBody
    {
        [JsonPropertyName("warehouseId")]
        public long WarehouseId { get; set; }

        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public long? OrderId { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class OutboundResponse
    {
        public long OperationId { get; set; }
        public long WarehouseId { get; set; }
        public string? ScannedReference { get; set; }
        public bool Matched { get; set; }
        public long? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? MatchStatus { get; set; }
        public string? LabelUrl { get; set; }
        public DateTimeOffset? OutboundReadyAt { get; set; }
    }

}
