using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.Core.Configuration;

namespace ObserwayLabelFlow.App.Services;

public sealed class AppApiBaseUrlProvider : IApiBaseUrlProvider
{
    private readonly IUserSettingsStore _userSettings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppApiBaseUrlProvider> _logger;
    private string _baseUrl = string.Empty;

    public AppApiBaseUrlProvider(
        IUserSettingsStore userSettings,
        IConfiguration configuration,
        ILogger<AppApiBaseUrlProvider> logger)
    {
        _userSettings = userSettings;
        _configuration = configuration;
        _logger = logger;
    }

    public string GetBaseUrl()
        => string.IsNullOrWhiteSpace(_baseUrl)
            ? _configuration["Api:BaseUrl"] ?? string.Empty
            : _baseUrl;

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _userSettings.LoadAsync(cancellationToken);
            _baseUrl = !string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
                ? settings.ApiBaseUrl.Trim()
                : _configuration["Api:BaseUrl"]?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API adresi yüklenirken hata oluştu.");
            _baseUrl = _configuration["Api:BaseUrl"]?.Trim() ?? string.Empty;
        }
    }
}
