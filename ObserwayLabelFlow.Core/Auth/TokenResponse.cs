namespace ObserwayLabelFlow.Core.Auth;

public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpireAt, long ExpiresInMs);

