using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ObserwayLabelFlow.Core.Auth;
using ObserwayLabelFlow.Core.Common;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Infrastructure.Http;

namespace ObserwayLabelFlow.Infrastructure.Auth;

public sealed class AuthApiClient(HttpClient http, IApiBaseUrlProvider apiBaseUrl) : IAuthApiClient
{
    public async Task<Result<TokenResponse>> LoginAsync(ApiLoginRequest request, CancellationToken ct = default)
    {
        var url = BuildUrl("/api/v1/auth/login");
        using var resp = await http.PostAsJsonAsync(url, new
        {
            username = request.Username,
            password = request.Password
        }, HttpJson.DefaultOptions, ct);

        if (resp.IsSuccessStatusCode)
        {
            var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(HttpJson.DefaultOptions, ct);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
                return Result<TokenResponse>.Fail("Sunucudan beklenmeyen yanıt alındı.");

            return Result<TokenResponse>.Success(token);
        }

        // Try to parse error payload
        var apiError = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>(HttpJson.DefaultOptions, ct);
        if (!string.IsNullOrWhiteSpace(apiError?.GetPrimaryMessage()))
            return Result<TokenResponse>.Fail(apiError.GetPrimaryMessage()!);

        if (apiError?.ErrorList.Count > 0)
            return Result<TokenResponse>.Fail(apiError.ErrorList.ToList());

        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            return Result<TokenResponse>.Fail("Kullanıcı adı veya parola hatalı.");
        }

        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return Result<TokenResponse>.Fail("Yetkisiz erişim. Kullanıcı adı/parola veya erişim kuralı hatalı olabilir.");

        return Result<TokenResponse>.Fail($"Giriş başarısız. (HTTP {(int)resp.StatusCode} {resp.ReasonPhrase})");
    }

    public async Task<Result<TokenResponse>> RefreshTokenAsync(string accessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUrl("/api/v1/auth/refresh-token"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        req.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var resp = await http.SendAsync(req, ct);

        if (resp.IsSuccessStatusCode)
        {
            var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(HttpJson.DefaultOptions, ct);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
                return Result<TokenResponse>.Fail("Token yenileme yanıtı beklenmeyen formatta.");

            return Result<TokenResponse>.Success(token);
        }

        var apiError = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>(HttpJson.DefaultOptions, ct);
        if (!string.IsNullOrWhiteSpace(apiError?.GetPrimaryMessage()))
            return Result<TokenResponse>.Fail(apiError.GetPrimaryMessage()!);

        if (apiError?.ErrorList.Count > 0)
            return Result<TokenResponse>.Fail(apiError.ErrorList.ToList());

        return Result<TokenResponse>.Fail($"Token yenileme başarısız. (HTTP {(int)resp.StatusCode} {resp.ReasonPhrase})");
    }

    private string BuildUrl(string relativePath)
    {
        var baseUrl = apiBaseUrl.GetBaseUrl().TrimEnd('/');
        var path = relativePath.StartsWith('/') ? relativePath : $"/{relativePath}";
        return $"{baseUrl}{path}";
    }
}

