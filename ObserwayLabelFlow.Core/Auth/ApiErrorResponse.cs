using System.Text.Json;

namespace ObserwayLabelFlow.Core.Auth;

public sealed class ApiErrorResponse
{
    public JsonElement Errors { get; set; }

    /// <summary>403-style payload: { "error": "Forbidden", "message": "..." }</summary>
    public string? Error { get; set; }

    public string? Message { get; set; }

    public IReadOnlyList<string> ErrorList
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Message))
                return new[] { Message.Trim() };

            if (Errors.ValueKind == JsonValueKind.String)
            {
                var text = Errors.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return new[] { text.Trim() };
            }

            if (Errors.ValueKind == JsonValueKind.Array)
            {
                return Errors.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(Error))
                return new[] { Error.Trim() };

            return Array.Empty<string>();
        }
    }

    public string? GetPrimaryMessage()
        => ErrorList.FirstOrDefault(static e => !string.IsNullOrWhiteSpace(e));
}
