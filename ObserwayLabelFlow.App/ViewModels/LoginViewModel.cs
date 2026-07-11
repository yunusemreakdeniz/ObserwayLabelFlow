using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.App.Services;
using ObserwayLabelFlow.Core.Auth;
using ObserwayLabelFlow.Core.Security;
using ObserwayLabelFlow.Infrastructure.Security;

namespace ObserwayLabelFlow.App.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthApiClient _auth;
    private readonly ITokenStore _tokenStore;
    private readonly ILoginPreferencesStore _loginPreferences;
    private readonly ILocalizationService _localization;
    private readonly IToastService _toasts;
    private readonly ILogger<LoginViewModel> _logger;
    private bool _suppressCultureSelection;

    public LoginViewModel(IAuthApiClient auth, ITokenStore tokenStore, ILoginPreferencesStore loginPreferences, ILocalizationService localization, IToastService toasts, ILogger<LoginViewModel> logger)
    {
        _auth = auth;
        _tokenStore = tokenStore;
        _loginPreferences = loginPreferences;
        _localization = localization;
        _toasts = toasts;
        _logger = logger;
        _localization.CultureChanged += OnLocalizationCultureChanged;

        var idx = LanguageOptions.Select((x, i) => new { x.Code, Index = i })
            .FirstOrDefault(x => x.Code == _localization.CurrentCultureName)?.Index ?? -1;
        _suppressCultureSelection = true;
        SelectedLanguageIndex = idx;
        _suppressCultureSelection = false;

        RefreshLoginButtonCaption();
    }

    public IReadOnlyList<CultureOption> LanguageOptions => _localization.LanguageOptions;

    [ObservableProperty]
    private int selectedLanguageIndex = -1;

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (_suppressCultureSelection || value < 0 || value >= LanguageOptions.Count)
            return;

        _ = ApplyLanguageAsync(LanguageOptions[value].Code);
    }

    private async Task ApplyLanguageAsync(string cultureCode)
    {
        try
        {
            await _localization.SetCultureAsync(cultureCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dil değişikliği uygulanırken hata oluştu.");
        }
    }

    private void OnLocalizationCultureChanged(object? sender, EventArgs e)
    {
        var idx = LanguageOptions.Select((x, i) => new { x.Code, Index = i })
            .FirstOrDefault(x => x.Code == _localization.CurrentCultureName)?.Index ?? -1;
        _suppressCultureSelection = true;
        SelectedLanguageIndex = idx;
        _suppressCultureSelection = false;
        RefreshLoginButtonCaption();
    }

    [ObservableProperty]
    private string loginButtonCaption = string.Empty;

    partial void OnIsBusyChanged(bool value)
    {
        RefreshLoginButtonCaption();
        LoginCommand.NotifyCanExecuteChanged();
    }

    private void RefreshLoginButtonCaption()
    {
        LoginButtonCaption = IsBusy
            ? _localization.Get("Login_SubmitBusy")
            : _localization.Get("Login_Submit");
    }

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool rememberMe = true;

    [ObservableProperty]
    private bool isBusy;

    public async Task InitializeAsync()
    {
        try
        {
            var prefs = await _loginPreferences.GetAsync();
            if (prefs is null)
            {
                RememberMe = true;
                return;
            }

            RememberMe = prefs.RememberMe;
            if (prefs.RememberMe && !string.IsNullOrWhiteSpace(prefs.Username))
                Username = prefs.Username.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Giriş tercihleri yüklenemedi.");
            RememberMe = true;
        }
    }

    private async Task SaveLoginPreferencesAsync()
    {
        await _loginPreferences.SaveAsync(new LoginPreferences(
            RememberMe,
            RememberMe ? Username.Trim() : string.Empty));
    }

    public ObservableCollection<string> Errors { get; } = new();

    public event Action? LoginSucceeded;

    private bool Validate()
    {
        Errors.Clear();

        if (string.IsNullOrWhiteSpace(Username))
            Errors.Add(_localization.Get("Validation_UsernameRequired"));
        else if (Username.Length > 50)
            Errors.Add(_localization.Get("Validation_UsernameMax"));

        if (string.IsNullOrWhiteSpace(Password))
            Errors.Add(_localization.Get("Validation_PasswordRequired"));
        else if (Password.Length < 6 || Password.Length > 100)
            Errors.Add(_localization.Get("Validation_PasswordLength"));

        return Errors.Count == 0;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        if (!Validate())
            return;

        IsBusy = true;

        try
        {
            var result = await _auth.LoginAsync(new ApiLoginRequest(Username.Trim(), Password));
            if (!result.IsSuccess || result.Value is null)
            {
                foreach (var err in result.Errors)
                    _toasts.Show(ToastKind.Warning, _localization.Get("Login_Title"), err);
                return;
            }

            var displayName =
                JwtClaimsReader.TryGetDisplayName(result.Value.AccessToken)
                ?? Username.Trim();

            await _tokenStore.SaveAsync(new AuthSession(
                result.Value.AccessToken,
                result.Value.ExpireAt,
                displayName,
                result.Value.ExpiresInMs));
            await SaveLoginPreferencesAsync();
            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Giriş yapılırken hata oluştu.");
            _toasts.Show(ToastKind.Error, _localization.Get("Login_Title"), _localization.Get("Error_Connection"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearSavedSessionAsync()
    {
        try
        {
            await _tokenStore.ClearAsync();
            await _loginPreferences.ClearAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kayıtlı oturum temizlenirken hata oluştu.");
        }

        Username = string.Empty;
        Password = string.Empty;
        RememberMe = false;
        Errors.Clear();
        _toasts.Show(ToastKind.Success, _localization.Get("Login_Title"), _localization.Get("SessionCleared"));
    }
}
