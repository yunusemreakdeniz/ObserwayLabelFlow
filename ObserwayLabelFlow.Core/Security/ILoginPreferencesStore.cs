namespace ObserwayLabelFlow.Core.Security;

public sealed record LoginPreferences(
    bool RememberMe,
    string Username);

public interface ILoginPreferencesStore
{
    Task<LoginPreferences?> GetAsync(CancellationToken ct = default);

    Task SaveAsync(LoginPreferences preferences, CancellationToken ct = default);

    Task ClearAsync(CancellationToken ct = default);
}
