using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.App.Data;

namespace ObserwayLabelFlow.App.Services;

public sealed class UserSettingsStore : IUserSettingsStore
{
    private const int SettingsRowId = 1;
    private const int MaxSaveAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private readonly IDbContextFactory<LabelFlowDbContext> _dbFactory;
    private readonly ILogger<UserSettingsStore> _logger;
    private readonly string _legacyJsonPath;

    public UserSettingsStore(IDbContextFactory<LabelFlowDbContext> dbFactory, ILogger<UserSettingsStore> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ObserwayLabelFlow");
        Directory.CreateDirectory(dir);
        _legacyJsonPath = Path.Combine(dir, "user-settings.json");
    }

    public async Task<UserAppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == SettingsRowId, cancellationToken);

        if (row is not null && !string.IsNullOrWhiteSpace(row.PayloadJson))
            return Deserialize(row.PayloadJson);

        if (File.Exists(_legacyJsonPath))
        {
            var legacy = await LoadLegacyJsonAsync(cancellationToken);
            await PersistAsync(db, legacy, cancellationToken);
            _logger.LogInformation("Eski user-settings.json ayarları SQLite veritabanına taşındı.");
            return legacy;
        }

        return new UserAppSettings();
    }

    public Task SaveAsync(UserAppSettings settings, CancellationToken cancellationToken = default)
    {
        return PersistWithRetryAsync(settings, cancellationToken);
    }

    private async Task PersistWithRetryAsync(UserAppSettings settings, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
                await PersistAsync(db, settings, cancellationToken);
                return;
            }
            catch (Exception ex) when (IsSqliteBusy(ex) && attempt < MaxSaveAttempts)
            {
                _logger.LogWarning(ex, "Ayarlar kaydedilirken SQLite meşgul, tekrar denenecek. Deneme={Attempt}", attempt);
                await Task.Delay(50 * attempt, cancellationToken);
            }
        }
    }

    private static bool IsSqliteBusy(Exception ex)
    {
        return ex is SqliteException { SqliteErrorCode: 5 or 6 }
            || ex.InnerException is SqliteException { SqliteErrorCode: 5 or 6 };
    }

    private async Task PersistAsync(LabelFlowDbContext db, UserAppSettings settings, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(settings, JsonOptions);
        var row = await db.UserSettings.FirstOrDefaultAsync(x => x.Id == SettingsRowId, cancellationToken);
        if (row is null)
        {
            db.UserSettings.Add(new UserSettingsRecord
            {
                Id = SettingsRowId,
                PayloadJson = payload,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            row.PayloadJson = payload;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Kullanıcı ayarları kaydedildi. Yazıcı={PrinterName}", settings.PrinterName);
    }

    private async Task<UserAppSettings> LoadLegacyJsonAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(_legacyJsonPath);
            var loaded = await JsonSerializer.DeserializeAsync<UserAppSettings>(stream, JsonOptions, cancellationToken);
            return loaded ?? new UserAppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Eski user-settings.json okunamadı.");
            return new UserAppSettings();
        }
    }

    private static UserAppSettings Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<UserAppSettings>(json, JsonOptions) ?? new UserAppSettings();
        }
        catch
        {
            return new UserAppSettings();
        }
    }
}
