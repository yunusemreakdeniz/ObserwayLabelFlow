using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.Warehouse;

namespace ObserwayLabelFlow.App.Services;

public sealed class WarehouseSession : IWarehouseSession
{
    private readonly IWarehouseApiClient _api;
    private readonly IUserSettingsStore _userSettings;
    private readonly ILogger<WarehouseSession> _logger;
    private IReadOnlyList<WarehouseDto> _available = Array.Empty<WarehouseDto>();
    private WarehouseDto? _selected;

    public WarehouseSession(
        IWarehouseApiClient api,
        IUserSettingsStore userSettings,
        ILogger<WarehouseSession> logger)
    {
        _api = api;
        _userSettings = userSettings;
        _logger = logger;
    }

    public WarehouseDto? Selected => _selected;

    public IReadOnlyList<WarehouseDto> Available => _available;

    public bool HasSelection => _selected is { Id: > 0 };

    public long SelectedWarehouseId => _selected?.Id ?? 0;

    public bool AllowInbound => _selected?.AllowInbound == true;

    public bool AllowOutbound => _selected?.AllowOutbound == true;

    public bool ShowProductDetailsOnInbound => _selected?.ShowProductDetailsOnInbound == true;

    public string SelectedDisplayName
    {
        get
        {
            if (_selected is null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(_selected.Name))
                return _selected.Name.Trim();
            return _selected.Code?.Trim() ?? string.Empty;
        }
    }

    public event EventHandler? Changed;

    public async Task ReloadAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await _api.GetMyWarehousesAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            _logger.LogWarning(
                "Depo listesi alınamadı: {Error}",
                result.Errors.FirstOrDefault() ?? "bilinmeyen");
            _available = Array.Empty<WarehouseDto>();
            _selected = null;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _available = result.Value
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        long preferredId = 0;
        try
        {
            var settings = await _userSettings.LoadAsync(cancellationToken);
            preferredId = settings.SelectedWarehouseId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Seçili depo ayarı okunamadı.");
        }

        WarehouseDto? next = null;
        if (preferredId > 0)
            next = _available.FirstOrDefault(w => w.Id == preferredId);

        if (next is null && _available.Count == 1)
            next = _available[0];

        if (next is null)
            next = _available.FirstOrDefault(w => w.IsDefault);

        if (next is not null && _available.Count == 1)
        {
            await SelectCoreAsync(next, persist: true, cancellationToken);
            return;
        }

        if (next is not null && preferredId == next.Id)
        {
            await SelectCoreAsync(next, persist: false, cancellationToken);
            return;
        }

        _selected = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SelectAsync(WarehouseDto warehouse, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        if (warehouse.Id <= 0)
            throw new ArgumentException("Geçersiz depo.", nameof(warehouse));

        await SelectCoreAsync(warehouse, persist: true, cancellationToken);
    }

    public async Task ClearSelectionAsync(CancellationToken cancellationToken = default)
    {
        _selected = null;
        try
        {
            var settings = await _userSettings.LoadAsync(cancellationToken);
            if (settings.SelectedWarehouseId != 0)
            {
                settings.SelectedWarehouseId = 0;
                await _userSettings.SaveAsync(settings, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Depo seçimi temizlenirken ayar yazılamadı.");
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task SelectCoreAsync(WarehouseDto warehouse, bool persist, CancellationToken cancellationToken)
    {
        _selected = warehouse;
        if (persist)
        {
            try
            {
                var settings = await _userSettings.LoadAsync(cancellationToken);
                if (settings.SelectedWarehouseId != warehouse.Id)
                {
                    settings.SelectedWarehouseId = warehouse.Id;
                    await _userSettings.SaveAsync(settings, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Seçili depo kaydedilemedi.");
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
