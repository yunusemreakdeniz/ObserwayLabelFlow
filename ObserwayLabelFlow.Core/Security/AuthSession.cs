namespace ObserwayLabelFlow.Core.Security;

public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset ExpireAt,
    string? DisplayName = null,
    long? ExpiresInMs = null);

