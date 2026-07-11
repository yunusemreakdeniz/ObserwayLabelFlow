namespace ObserwayLabelFlow.App.Services;

public interface ILocalizationService
{
    IReadOnlyList<CultureOption> LanguageOptions { get; }

    string CurrentCultureName { get; }

    event EventHandler? CultureChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default);

    string Get(string key, params object?[]? args);
}
