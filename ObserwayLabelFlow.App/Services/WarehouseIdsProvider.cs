using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.Core.Configuration;

namespace ObserwayLabelFlow.App.Services;

public sealed class WarehouseIdsProvider : IWarehouseIdsProvider
{
    private readonly IConfiguration _configuration;
    private readonly IUserSettingsStore _userSettings;
    private readonly ILogger<WarehouseIdsProvider> _logger;
    private long? _texasOverride;
    private long? _mexicoOverride;

    public WarehouseIdsProvider(
        IConfiguration configuration,
        IUserSettingsStore userSettings,
        ILogger<WarehouseIdsProvider> logger)
    {
        _configuration = configuration;
        _userSettings = userSettings;
        _logger = logger;
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _userSettings.LoadAsync(cancellationToken);
            _texasOverride = settings.TexasWarehouseId > 0 ? settings.TexasWarehouseId : null;
            _mexicoOverride = settings.MexicoWarehouseId > 0 ? settings.MexicoWarehouseId : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Depo Id ayarları yüklenirken hata oluştu.");
            _texasOverride = null;
            _mexicoOverride = null;
        }
    }

    public long GetTexasWarehouseId()
        => _texasOverride
           ?? ReadConfigId("Warehouse:TexasWarehouseId")
           ?? 0;

    public long GetMexicoWarehouseId()
        => _mexicoOverride
           ?? ReadConfigId("Warehouse:MexicoWarehouseId")
           ?? 0;

    private long? ReadConfigId(string key)
    {
        var raw = _configuration[key];
        if (long.TryParse(raw, out var id) && id > 0)
            return id;
        return null;
    }
}
