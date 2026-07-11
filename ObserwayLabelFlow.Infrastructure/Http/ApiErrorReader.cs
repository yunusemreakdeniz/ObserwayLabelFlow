using System.Net.Http.Json;
using System.Text.Json;
using ObserwayLabelFlow.Core.Auth;

namespace ObserwayLabelFlow.Infrastructure.Http;

internal static class ApiErrorReader
{
    public static async Task<string> ReadFirstErrorAsync(HttpContent? content, string fallback, CancellationToken ct)
    {
        if (content is null)
            return fallback;

        try
        {
            var payload = await content.ReadFromJsonAsync<ApiErrorResponse>(HttpJson.DefaultOptions, ct);
            var message = payload?.GetPrimaryMessage();
            if (!string.IsNullOrWhiteSpace(message))
                return message;
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return fallback;
    }
}
