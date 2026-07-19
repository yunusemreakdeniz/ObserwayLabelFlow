using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.Core.Common;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.Orders;
using ObserwayLabelFlow.Core.Security;
using ObserwayLabelFlow.Infrastructure.Http;

namespace ObserwayLabelFlow.Infrastructure.Orders;

public sealed class OrdersApiClient(
    HttpClient http,
    ITokenStore tokenStore,
    IApiBaseUrlProvider apiBaseUrl,
    ILogger<OrdersApiClient> logger) : IOrdersApiClient
{
    public async Task<Result<OrderDto>> GetOrderByTrackingNumberAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return Result<OrderDto>.Fail("Referans kodu zorunludur (OBS sipariş numarası veya satın alma takip numarası).");

        var session = await tokenStore.GetAsync(ct);
        if (session?.AccessToken is null)
            return Result<OrderDto>.Fail("Oturum bilgisi bulunamadı.");

        var baseAddress = apiBaseUrl.GetBaseUrl().TrimEnd('/');
        var url = $"{baseAddress}/api/v1/orders/get-order-by-tracking-number?reference={Uri.EscapeDataString(reference.Trim())}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken.Trim());

        using var resp = await http.SendAsync(req, ct);

        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return Result<OrderDto>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            OrderDto? order;
            try
            {
                order = OrderDtoJson.Parse(json);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sipariş JSON parse edilemedi. Shape={Shape}", OrderDtoJson.DescribeProductShape(json));
                return Result<OrderDto>.Fail("Sunucudan beklenmeyen yanıt alındı.");
            }

            if (order is null)
                return Result<OrderDto>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            var productCount = order.GetProducts().Count;
            if (productCount == 0)
            {
                logger.LogWarning(
                    "Sipariş ürün listesi boş. Reference={Reference} Order={Order} Shape={Shape}",
                    reference,
                    order.ObserwayOrderNumber,
                    OrderDtoJson.DescribeProductShape(json));
            }
            else
            {
                logger.LogInformation(
                    "Sipariş okundu. Reference={Reference} Order={Order} Products={ProductCount}",
                    reference,
                    order.ObserwayOrderNumber,
                    productCount);
            }

            return Result<OrderDto>.Success(order);
        }

        return resp.StatusCode switch
        {
            HttpStatusCode.BadRequest => Result<OrderDto>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Geçersiz istek.", ct)),
            HttpStatusCode.Unauthorized => Result<OrderDto>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Oturum süresi doldu veya geçersiz token. Lütfen tekrar giriş yapın.", ct)),
            HttpStatusCode.Forbidden => Result<OrderDto>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Bu işlem için yetkiniz yok.", ct)),
            HttpStatusCode.NotFound => Result<OrderDto>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Sipariş bulunamadı.", ct)),
            _ => Result<OrderDto>.Fail($"Sorgu başarısız. (HTTP {(int)resp.StatusCode} {resp.ReasonPhrase})"),
        };
    }

    public async Task<Result<bool>> MarkOutboundReadyAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return Result<bool>.Fail("Referans kodu zorunludur (OBS sipariş numarası veya satın alma takip numarası).");

        var session = await tokenStore.GetAsync(ct);
        if (session?.AccessToken is null)
            return Result<bool>.Fail("Oturum bilgisi bulunamadı.");

        var baseAddress = apiBaseUrl.GetBaseUrl().TrimEnd('/');
        var url = $"{baseAddress}/api/v1/orders/mark-outbound-ready?reference={Uri.EscapeDataString(reference.Trim())}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken.Trim());

        using var resp = await http.SendAsync(req, ct);

        // 204 No Content (idempotent); bazı ortamlarda 200 da gelebilir.
        if (resp.StatusCode is HttpStatusCode.NoContent || resp.IsSuccessStatusCode)
            return Result<bool>.Success(true);

        return resp.StatusCode switch
        {
            HttpStatusCode.BadRequest => Result<bool>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Geçersiz istek.", ct)),
            HttpStatusCode.Unauthorized => Result<bool>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Oturum süresi doldu veya geçersiz token. Lütfen tekrar giriş yapın.", ct)),
            HttpStatusCode.Forbidden => Result<bool>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Bu işlem için yetkiniz yok.", ct)),
            HttpStatusCode.NotFound => Result<bool>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Sipariş bulunamadı.", ct)),
            _ => Result<bool>.Fail($"Çıkış işaretleme başarısız. (HTTP {(int)resp.StatusCode} {resp.ReasonPhrase})"),
        };
    }
}
