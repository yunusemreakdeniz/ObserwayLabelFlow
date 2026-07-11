using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ObserwayLabelFlow.Core.Security;

namespace ObserwayLabelFlow.Infrastructure.Security;

public sealed class DpapiLoginPreferencesStore : ILoginPreferencesStore
{
    private const string FileName = "login_preferences.dat";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string GetFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ObserwayLabelFlow");

        Directory.CreateDirectory(dir);
        return Path.Combine(dir, FileName);
    }

    public async Task<LoginPreferences?> GetAsync(CancellationToken ct = default)
    {
        var path = GetFilePath();
        if (!File.Exists(path))
            return null;

        var protectedBytes = await File.ReadAllBytesAsync(path, ct);
        if (protectedBytes.Length == 0)
            return null;

        var jsonBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(jsonBytes);

        return JsonSerializer.Deserialize<LoginPreferences>(json, JsonOptions);
    }

    public async Task SaveAsync(LoginPreferences preferences, CancellationToken ct = default)
    {
        var path = GetFilePath();
        var json = JsonSerializer.Serialize(preferences, JsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(jsonBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(path, protectedBytes, ct);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        var path = GetFilePath();
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }
}
