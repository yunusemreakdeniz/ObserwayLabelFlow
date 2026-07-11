using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ObserwayLabelFlow.Core.Common;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.Orders;
using ObserwayLabelFlow.Core.Security;
using ObserwayLabelFlow.Infrastructure.Http;

namespace ObserwayLabelFlow.Infrastructure.Orders;

public sealed class OrdersApiClient(HttpClient http, ITokenStore tokenStore, IApiBaseUrlProvider apiBaseUrl) : IOrdersApiClient
{
    public async Task<Result<OrderDto>> GetOrderByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return Result<OrderDto>.Fail("Takip numarası boş olamaz.");

        var session = await tokenStore.GetAsync(ct);
        if (session?.AccessToken is null)
            return Result<OrderDto>.Fail("Oturum bilgisi bulunamadı.");

        var baseAddress = apiBaseUrl.GetBaseUrl().TrimEnd('/');
        var url = $"{baseAddress}/api/v1/orders/get-order-by-tracking-number?tracking-number={Uri.EscapeDataString(trackingNumber.Trim())}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken.Trim());

        using var resp = await http.SendAsync(req, ct);

        if (resp.IsSuccessStatusCode)
        {
            var order = await resp.Content.ReadFromJsonAsync<OrderDto>(HttpJson.DefaultOptions, ct);
            if (order is null)
                return Result<OrderDto>.Fail("Sunucudan beklenmeyen yanıt alındı.");

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
}
