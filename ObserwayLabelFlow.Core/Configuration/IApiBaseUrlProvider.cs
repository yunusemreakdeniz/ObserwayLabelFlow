namespace ObserwayLabelFlow.Core.Configuration;

public interface IApiBaseUrlProvider
{
    string GetBaseUrl();

    Task ReloadAsync(CancellationToken cancellationToken = default);
}
