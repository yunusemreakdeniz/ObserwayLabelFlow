using System.Reflection;

namespace ObserwayLabelFlow.App.Infrastructure;

/// <summary>
/// Application semantic version: major (breaking) . minor (feature) . fix (patch).
/// Source: csproj &lt;Version&gt; / &lt;InformationalVersion&gt;.
/// </summary>
public static class AppVersionInfo
{
    public static string SemanticVersion { get; } = ReadSemanticVersion();

    public static string Display => $"v{SemanticVersion}";

    private static string ReadSemanticVersion()
    {
        var assembly = typeof(AppVersionInfo).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var core = informational.Split('+', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (IsSemanticVersion(core))
                return core;
        }

        var version = assembly.GetName().Version;
        if (version is null)
            return "0.0.0";

        var fix = version.Revision > 0 ? version.Revision : version.Build;
        return $"{version.Major}.{version.Minor}.{fix}";
    }

    private static bool IsSemanticVersion(string value)
    {
        var parts = value.Split('.');
        if (parts.Length < 2)
            return false;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out _))
                return false;
        }

        return true;
    }
}
