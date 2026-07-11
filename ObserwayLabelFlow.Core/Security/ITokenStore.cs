namespace ObserwayLabelFlow.Core.Security;

public interface ITokenStore
{
    Task<AuthSession?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(AuthSession session, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

