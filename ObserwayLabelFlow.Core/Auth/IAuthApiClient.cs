using ObserwayLabelFlow.Core.Common;

namespace ObserwayLabelFlow.Core.Auth;

public interface IAuthApiClient
{
    Task<Result<TokenResponse>> LoginAsync(ApiLoginRequest request, CancellationToken ct = default);

    Task<Result<TokenResponse>> RefreshTokenAsync(string accessToken, CancellationToken ct = default);
}

