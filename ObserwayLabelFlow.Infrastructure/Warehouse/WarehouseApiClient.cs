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
    public async Task<Result<WarehouseLookupDto>> LookupAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return Result<WarehouseLookupDto>.Fail("Referans kodu zorunludur (OBS sipariş numarası veya satın alma takip numarası).");

        var auth = await TryAuthorizeAsync(ct);
        if (auth is null)
            return Result<WarehouseLookupDto>.Fail("Oturum bilgisi bulunamadı.");

        var url = $"{Base()}/api/v1/warehouse/lookup?reference={Uri.EscapeDataString(reference.Trim())}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);

        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var payload = await resp.Content.ReadFromJsonAsync<WarehouseLookupDto>(HttpJson.DefaultOptions, ct);
            if (payload is null || payload.OrderId <= 0)
                return Result<WarehouseLookupDto>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            logger.LogInformation(
                "Warehouse lookup. Reference={Reference} Order={Order} Status={Status} Products={Count}",
                reference,
                payload.OrderNumber,
                payload.OrderStatusDisplay,
                payload.Products?.Count ?? 0);

            payload.Products ??= new List<WarehouseProductDto>();
            return Result<WarehouseLookupDto>.Success(payload);
        }

        return await FailAsync<WarehouseLookupDto>(resp, "Sorgu başarısız.", ct);
    }

    public async Task<Result<WarehouseInboundResult>> MarkInboundReceivedAsync(
        long orderId,
        WarehouseInboundRequest request,
        CancellationToken ct = default)
    {
        if (orderId <= 0)
            return Result<WarehouseInboundResult>.Fail("Sipariş kimliği geçersiz.");
        if (request.WarehouseId <= 0)
            return Result<WarehouseInboundResult>.Fail("Depo kimliği (warehouseId) tanımlı değil.");

        var auth = await TryAuthorizeAsync(ct);
        if (auth is null)
            return Result<WarehouseInboundResult>.Fail("Oturum bilgisi bulunamadı.");

        var url = $"{Base()}/api/v1/warehouse/orders/{orderId}/inbound-received";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        req.Content = JsonContent.Create(new InboundBody
        {
            WarehouseId = request.WarehouseId,
            InboundTrackingNumber = request.InboundTrackingNumber,
            InboundCarrier = request.InboundCarrier,
            Note = request.Note
        }, options: HttpJson.DefaultOptions);

        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var payload = await resp.Content.ReadFromJsonAsync<InboundResponse>(HttpJson.DefaultOptions, ct);
            if (payload is null || string.IsNullOrWhiteSpace(payload.OrderNumber))
                return Result<WarehouseInboundResult>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            return Result<WarehouseInboundResult>.Success(new WarehouseInboundResult(
                payload.OrderId,
                payload.OrderNumber.Trim(),
                payload.OperationId,
                payload.InboundReceivedAt));
        }

        return await FailAsync<WarehouseInboundResult>(resp, "Depo girişi başarısız.", ct);
    }

    public async Task<Result<WarehouseLoadToVehicleResult>> LoadToVehicleAsync(
        long orderId,
        WarehouseLoadToVehicleRequest request,
        CancellationToken ct = default)
    {
        if (orderId <= 0)
            return Result<WarehouseLoadToVehicleResult>.Fail("Sipariş kimliği geçersiz.");
        if (request.WarehouseId <= 0)
            return Result<WarehouseLoadToVehicleResult>.Fail("Depo kimliği (warehouseId) tanımlı değil.");
        if (string.IsNullOrWhiteSpace(request.VehicleName))
            return Result<WarehouseLoadToVehicleResult>.Fail("Araç adı zorunludur.");

        var auth = await TryAuthorizeAsync(ct);
        if (auth is null)
            return Result<WarehouseLoadToVehicleResult>.Fail("Oturum bilgisi bulunamadı.");

        var url = $"{Base()}/api/v1/warehouse/orders/{orderId}/load-to-vehicle";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        req.Content = JsonContent.Create(new LoadBody
        {
            WarehouseId = request.WarehouseId,
            VehicleName = request.VehicleName.Trim(),
            Note = request.Note
        }, options: HttpJson.DefaultOptions);

        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var payload = await resp.Content.ReadFromJsonAsync<LoadResponse>(HttpJson.DefaultOptions, ct);
            if (payload is null || string.IsNullOrWhiteSpace(payload.OrderNumber))
                return Result<WarehouseLoadToVehicleResult>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            return Result<WarehouseLoadToVehicleResult>.Success(new WarehouseLoadToVehicleResult(
                payload.OrderId,
                payload.OrderNumber.Trim(),
                payload.OperationId,
                payload.VehicleName?.Trim() ?? request.VehicleName.Trim(),
                payload.CarrierName,
                payload.CarrierCode));
        }

        return await FailAsync<WarehouseLoadToVehicleResult>(resp, "Araca yükleme başarısız.", ct);
    }

    public async Task<Result<WarehouseOutboundResult>> MarkOutboundReadyAsync(
        long orderId,
        WarehouseOutboundRequest request,
        CancellationToken ct = default)
    {
        if (orderId <= 0)
            return Result<WarehouseOutboundResult>.Fail("Sipariş kimliği geçersiz.");
        if (request.WarehouseId <= 0)
            return Result<WarehouseOutboundResult>.Fail("Depo kimliği (warehouseId) tanımlı değil.");

        var auth = await TryAuthorizeAsync(ct);
        if (auth is null)
            return Result<WarehouseOutboundResult>.Fail("Oturum bilgisi bulunamadı.");

        var url = $"{Base()}/api/v1/warehouse/orders/{orderId}/outbound-ready";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        req.Content = JsonContent.Create(new OutboundBody
        {
            WarehouseId = request.WarehouseId,
            Note = request.Note
        }, options: HttpJson.DefaultOptions);

        using var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode is HttpStatusCode.NoContent || resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode is HttpStatusCode.NoContent
                || resp.Content.Headers.ContentLength is 0)
            {
                return Result<WarehouseOutboundResult>.Success(new WarehouseOutboundResult(
                    orderId,
                    string.Empty,
                    0,
                    DateTimeOffset.UtcNow));
            }

            var payload = await resp.Content.ReadFromJsonAsync<OutboundResponse>(HttpJson.DefaultOptions, ct);
            if (payload is null)
                return Result<WarehouseOutboundResult>.Success(new WarehouseOutboundResult(
                    orderId,
                    string.Empty,
                    0,
                    DateTimeOffset.UtcNow));

            return Result<WarehouseOutboundResult>.Success(new WarehouseOutboundResult(
                payload.OrderId,
                payload.OrderNumber?.Trim() ?? string.Empty,
                payload.OperationId,
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
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Sipariş bulunamadı.", ct)),
            _ => Result<T>.Fail($"{fallbackPrefix} (HTTP {(int)resp.StatusCode} {resp.ReasonPhrase})"),
        };
    }

    private sealed class InboundBody
    {
        [JsonPropertyName("warehouseId")]
        public long WarehouseId { get; set; }

        [JsonPropertyName("inboundTrackingNumber")]
        public string? InboundTrackingNumber { get; set; }

        [JsonPropertyName("inboundCarrier")]
        public string? InboundCarrier { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class InboundResponse
    {
        public long OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public long OperationId { get; set; }
        public DateTimeOffset? InboundReceivedAt { get; set; }
    }

    private sealed class LoadBody
    {
        [JsonPropertyName("warehouseId")]
        public long WarehouseId { get; set; }

        [JsonPropertyName("vehicleName")]
        public string VehicleName { get; set; } = string.Empty;

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class LoadResponse
    {
        public long OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public long OperationId { get; set; }
        public string? VehicleName { get; set; }
        public string? CarrierName { get; set; }
        public string? CarrierCode { get; set; }
    }

    private sealed class OutboundBody
    {
        [JsonPropertyName("warehouseId")]
        public long WarehouseId { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class OutboundResponse
    {
        public long OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public long OperationId { get; set; }
        public DateTimeOffset? OutboundReadyAt { get; set; }
    }
}
