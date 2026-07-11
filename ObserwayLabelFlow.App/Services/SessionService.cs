using Microsoft.Extensions.Configuration;
using ObserwayLabelFlow.Core.Auth;
using ObserwayLabelFlow.Core.Security;

namespace ObserwayLabelFlow.App.Services;

public sealed class SessionService(
    ITokenStore tokenStore,
    IAuthApiClient auth,
    IConfiguration configuration) : ISessionService
{
    public async Task<bool> RestoreOrRefreshAsync(CancellationToken ct = default)
    {
        var session = await tokenStore.GetAsync(ct);
        if (session is null)
            return false;

        return await TryRefreshIfNeededAsync(session, ct);
    }

    public async Task<bool> TryMaintainSessionAsync(CancellationToken ct = default)
    {
        var session = await tokenStore.GetAsync(ct);
        if (session is null)
            return false;

        return await TryRefreshIfNeededAsync(session, ct);
    }

    private async Task<bool> TryRefreshIfNeededAsync(AuthSession session, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var refreshBeforeMinutes = configuration.GetValue("Session:RefreshBeforeExpiryMinutes", 10);
        var margin = TimeSpan.FromMinutes(refreshBeforeMinutes);

        var remaining = session.ExpireAt - now;
        var needsRefresh = remaining <= margin;

        if (!needsRefresh)
            return true;

        var result = await auth.RefreshTokenAsync(session.AccessToken, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            await tokenStore.ClearAsync(ct);
            return false;
        }

        await tokenStore.SaveAsync(session with
        {
            AccessToken = result.Value.AccessToken,
            ExpireAt = result.Value.ExpireAt,
            ExpiresInMs = result.Value.ExpiresInMs
        }, ct);

        return true;
    }
}
