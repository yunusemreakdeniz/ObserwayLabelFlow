using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ObserwayLabelFlow.Core.Common;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.Inbound;
using ObserwayLabelFlow.Core.Security;
using ObserwayLabelFlow.Infrastructure.Http;

namespace ObserwayLabelFlow.Infrastructure.Inbound;

public sealed class InboundApiClient(HttpClient http, ITokenStore tokenStore, IApiBaseUrlProvider apiBaseUrl) : IInboundApiClient
{
    public async Task<Result<InboundMarkResult>> MarkInboundReceivedAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return Result<InboundMarkResult>.Fail("Referans kodu zorunludur (OBS sipariş numarası veya satın alma takip numarası).");

        var session = await tokenStore.GetAsync(ct);
        if (session?.AccessToken is null)
            return Result<InboundMarkResult>.Fail("Oturum bilgisi bulunamadı.");

        var baseAddress = apiBaseUrl.GetBaseUrl().TrimEnd('/');
        var url = $"{baseAddress}/api/v1/orders/mark-inbound-received?reference={Uri.EscapeDataString(reference.Trim())}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken.Trim());

        using var resp = await http.SendAsync(req, ct);

        if (resp.IsSuccessStatusCode)
        {
            var payload = await resp.Content.ReadFromJsonAsync<InboundMarkReceivedResponse>(HttpJson.DefaultOptions, ct);
            if (payload is null || string.IsNullOrWhiteSpace(payload.OrderNumber))
                return Result<InboundMarkResult>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            return Result<InboundMarkResult>.Success(new InboundMarkResult(payload.OrderNumber.Trim()));
        }

        return resp.StatusCode switch
        {
            HttpStatusCode.BadRequest => Result<InboundMarkResult>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Geçersiz istek.", ct)),
            HttpStatusCode.Unauthorized => Result<InboundMarkResult>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Oturum süresi doldu veya geçersiz token. Lütfen tekrar giriş yapın.", ct)),
            HttpStatusCode.Forbidden => Result<InboundMarkResult>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Bu işlem için yetkiniz yok.", ct)),
            HttpStatusCode.NotFound => Result<InboundMarkResult>.Fail(
                await ApiErrorReader.ReadFirstErrorAsync(resp.Content, "Sipariş bulunamadı.", ct)),
            _ => Result<InboundMarkResult>.Fail($"Depo girişi başarısız. (HTTP {(int)resp.StatusCode} {resp.ReasonPhrase})"),
        };
    }

    private sealed class InboundMarkReceivedResponse
    {
        public string OrderNumber { get; set; } = string.Empty;
    }
}
