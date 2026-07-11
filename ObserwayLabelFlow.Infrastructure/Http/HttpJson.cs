using System.Net.Http.Json;
using System.Text.Json;

namespace ObserwayLabelFlow.Infrastructure.Http;

internal static class HttpJson
{
    public static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static Task<HttpResponseMessage> PostAsJsonAsync<T>(
        this HttpClient http,
        string requestUri,
        T value,
        CancellationToken ct)
        => http.PostAsJsonAsync(requestUri, value, DefaultOptions, ct);

    public static Task<T?> ReadFromJsonAsync<T>(this HttpContent content, CancellationToken ct)
        => content.ReadFromJsonAsync<T>(DefaultOptions, ct);
}

