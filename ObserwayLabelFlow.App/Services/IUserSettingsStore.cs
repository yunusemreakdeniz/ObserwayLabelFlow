namespace ObserwayLabelFlow.App.Services;

public interface IUserSettingsStore
{
    Task<UserAppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserAppSettings settings, CancellationToken cancellationToken = default);
}
