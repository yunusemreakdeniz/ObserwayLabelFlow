using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ObserwayLabelFlow.App.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly IConfiguration _configuration;
    private readonly IUserSettingsStore _userSettings;
    private readonly ILogger<LocalizationService> _logger;

    private Dictionary<string, Dictionary<string, string>> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _allKeys = new(StringComparer.Ordinal);
    private IReadOnlyList<CultureOption> _languageOptions = Array.Empty<CultureOption>();

    public LocalizationService(IConfiguration configuration, IUserSettingsStore userSettings, ILogger<LocalizationService> logger)
    {
        _configuration = configuration;
        _userSettings = userSettings;
        _logger = logger;
    }

    public IReadOnlyList<CultureOption> LanguageOptions => _languageOptions;

    public string CurrentCultureName { get; private set; } = "tr-TR";

    public event EventHandler? CultureChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LoadCatalogFromEmbeddedJson();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lokalizasyon kataloğu yüklenirken hata oluştu.");
            throw;
        }

        BuildLanguageOptions();

        var user = await _userSettings.LoadAsync(cancellationToken);
        var picked = PickSupportedCulture(user.UiCulture);
        await ApplyCultureCoreAsync(picked, cancellationToken);

        if (!string.Equals(user.UiCulture, picked, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                user.UiCulture = picked;
                await _userSettings.SaveAsync(user, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Varsayılan dil ayarlarına kaydedilemedi.");
            }
        }

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
                CultureChanged?.Invoke(this, EventArgs.Empty);
            else
                await dispatcher.InvokeAsync(() => CultureChanged?.Invoke(this, EventArgs.Empty));
        }
        else
            CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        var picked = PickSupportedCulture(cultureName);
        await ApplyCultureCoreAsync(picked, cancellationToken);

        try
        {
            var current = await _userSettings.LoadAsync(cancellationToken);
            current.UiCulture = picked;
            await _userSettings.SaveAsync(current, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dil ayarı kaydedilirken hata oluştu.");
        }

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
                CultureChanged?.Invoke(this, EventArgs.Empty);
            else
                await dispatcher.InvokeAsync(() => CultureChanged?.Invoke(this, EventArgs.Empty));
        }
        else
            CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key, params object?[]? args)
    {
        var raw = ResolveRaw(key);
        return args is { Length: > 0 }
            ? string.Format(CultureInfo.CurrentUICulture, raw, args)
            : raw;
    }

    private void LoadCatalogFromEmbeddedJson()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("ui-strings.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new InvalidOperationException("Gömülü ui-strings.json bulunamadı.");

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("ui-strings.json akışı açılamadı.");
        using var doc = JsonDocument.Parse(stream);

        _catalog = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        _allKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var cultureProp in doc.RootElement.EnumerateObject())
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in cultureProp.Value.EnumerateObject())
            {
                map[kv.Name] = kv.Value.GetString() ?? string.Empty;
                _allKeys.Add(kv.Name);
            }

            _catalog[cultureProp.Name] = map;
        }

        if (!_catalog.ContainsKey("tr-TR"))
            throw new InvalidOperationException("ui-strings.json içinde tr-TR zorunludur.");
    }

    private void BuildLanguageOptions()
    {
        var supported = _configuration.GetSection("Localization:SupportedCultures").Get<string[]>()
                        ?? new[] { "tr-TR", "en-US" };

        var list = new List<CultureOption>();
        foreach (var code in supported)
        {
            if (!_catalog.ContainsKey(code))
                continue;

            try
            {
                var ci = CultureInfo.GetCultureInfo(code);
                list.Add(new CultureOption
                {
                    Code = ci.Name,
                    DisplayName = ci.NativeName
                });
            }
            catch (CultureNotFoundException ex)
            {
                _logger.LogWarning(ex, "Geçersiz dil kodu atlandı: {CultureCode}", code);
            }
        }

        _languageOptions = list.Count > 0
            ? list
            : new List<CultureOption>
            {
                new() { Code = "tr-TR", DisplayName = "Türkçe" }
            };
    }

    private string PickSupportedCulture(string? requested)
    {
        var fallback = _configuration["Localization:DefaultCulture"];
        if (string.IsNullOrWhiteSpace(fallback))
            fallback = "tr-TR";

        requested = requested?.Trim();
        if (!string.IsNullOrWhiteSpace(requested) && _catalog.ContainsKey(requested))
            return CultureInfo.GetCultureInfo(requested).Name;

        if (_catalog.ContainsKey(fallback))
            return CultureInfo.GetCultureInfo(fallback).Name;

        return "tr-TR";
    }

    private async Task ApplyCultureCoreAsync(string cultureName, CancellationToken cancellationToken)
    {
        CurrentCultureName = cultureName;
        var ci = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentUICulture = ci;
        CultureInfo.CurrentCulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci;
        CultureInfo.DefaultThreadCurrentCulture = ci;

        if (Application.Current is null)
            return;

        void ApplyResources()
        {
            foreach (var key in _allKeys)
            {
                var text = ResolveRaw(key);
                Application.Current!.Resources[$"Loc_{key}"] = text;
            }
        }

        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
            ApplyResources();
        else
            await dispatcher.InvokeAsync(ApplyResources, DispatcherPriority.Normal, cancellationToken);
    }

    private string ResolveRaw(string key)
    {
        if (_catalog.TryGetValue(CurrentCultureName, out var map) &&
            map.TryGetValue(key, out var v) &&
            !string.IsNullOrEmpty(v))
            return v;

        if (_catalog.TryGetValue("tr-TR", out var tr) &&
            tr.TryGetValue(key, out var tv) &&
            !string.IsNullOrEmpty(tv))
            return tv;

        return key;
    }
}
