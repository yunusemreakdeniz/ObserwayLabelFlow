using System.Text;
using System.Text.Json;

namespace ObserwayLabelFlow.Infrastructure.Security;

public static class JwtClaimsReader
{
    public static string? TryGetDisplayName(string jwt)
    {
        try
        {
            var payloadJson = TryReadPayloadJson(jwt);
            if (string.IsNullOrWhiteSpace(payloadJson))
                return null;

            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            // Common claim names across templates
            foreach (var name in new[]
                     {
                         "name",
                         "given_name",
                         "unique_name",
                         "preferred_username",
                         "sub",
                         "email",
                         "username"
                     })
            {
                if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                {
                    var s = p.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadPayloadJson(string jwt)
    {
        var parts = jwt.Split('.', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return null;

        var payload = parts[1];
        var bytes = Base64UrlDecode(payload);
        if (bytes.Length == 0)
            return null;

        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        try
        {
            return Convert.FromBase64String(s);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}
