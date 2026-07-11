using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using ObserwayLabelFlow.App.Data;
using ObserwayLabelFlow.App.Services;
using ObserwayLabelFlow.App.ViewModels;
using ObserwayLabelFlow.App.Views;
using ObserwayLabelFlow.Core.Auth;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.History;
using ObserwayLabelFlow.Core.Orders;
using ObserwayLabelFlow.Core.Security;
using ObserwayLabelFlow.Infrastructure.Auth;
using ObserwayLabelFlow.Infrastructure.Orders;
using ObserwayLabelFlow.Infrastructure.Security;

namespace ObserwayLabelFlow.App;

public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host başlatılmadı.");

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.SetBasePath(AppContext.BaseDirectory);
                    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((ctx, services) =>
                {
                    var baseUrl = ctx.Configuration["Api:BaseUrl"];
                    if (string.IsNullOrWhiteSpace(baseUrl))
                        throw new InvalidOperationException("Api:BaseUrl tanımlı değil. appsettings.json'u kontrol et.");

                    var allowInvalidCerts = bool.TryParse(ctx.Configuration["Api:AllowInvalidCerts"], out var b) && b;

                    services.AddSingleton<IApiBaseUrlProvider, AppApiBaseUrlProvider>();
                    services.AddSingleton<IHistoryExportService, HistoryExcelExportService>();
                    services.AddSingleton<IToastService, ToastService>();
                    services.AddSingleton<IAppDialogService, AppDialogService>();
                    services.AddSingleton<ITokenStore, DpapiTokenStore>();
                    services.AddSingleton<ILoginPreferencesStore, DpapiLoginPreferencesStore>();
                    services.AddSingleton<IUserSettingsStore, UserSettingsStore>();
                    services.AddSingleton<ILocalizationService, LocalizationService>();

                    services.AddHttpClient<IAuthApiClient, AuthApiClient>(http =>
                    {
                        http.BaseAddress = new Uri(baseUrl);
                        http.Timeout = TimeSpan.FromSeconds(30);
                    })
                    .ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        var handler = new HttpClientHandler();
                        if (allowInvalidCerts)
                        {
                            handler.ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        }
                        return handler;
                    });

                    services.AddHttpClient<IOrdersApiClient, OrdersApiClient>(http =>
                    {
                        http.BaseAddress = new Uri(baseUrl);
                        http.Timeout = TimeSpan.FromSeconds(30);
                    })
                    .ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        var handler = new HttpClientHandler();
                        if (allowInvalidCerts)
                        {
                            handler.ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        }
                        return handler;
                    });

                    services.AddHttpClient<ILabelPdfLoader, LabelPdfLoader>(http =>
                    {
                        http.Timeout = TimeSpan.FromSeconds(60);
                    })
                    .ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        var handler = new HttpClientHandler();
                        if (allowInvalidCerts)
                        {
                            handler.ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        }
                        return handler;
                    });

                    services.AddSingleton<ILabelPrintService, LabelPrintService>();

                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddTransient<Views.SettingsWindow>();
                    services.AddTransient<ViewModels.SettingsViewModel>();

                    var dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ObserwayLabelFlow",
                        "labelflow.db");

                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                    services.AddDbContextFactory<LabelFlowDbContext>(o =>
                        o.UseSqlite($"Data Source={dbPath};Cache=Shared"));

                    services.AddSingleton<IHistoryService, HistoryService>();
                    services.AddSingleton<ISessionService, SessionService>();
                })
                .Build();

            _host.Start();
            var logger = Services.GetRequiredService<ILogger<App>>();

            using (var db = Services.GetRequiredService<IDbContextFactory<LabelFlowDbContext>>().CreateDbContext())
            {
                // Eski EnsureCreated veritabanları migration tablosu içermez; bu durumda migrate edilemez.
                // Otomatik silip yeniden oluşturuyoruz (geçmiş verisi kaybolur).
                if (!db.Database.GetAppliedMigrations().Any())
                {
                    var connectionString = db.Database.GetDbConnection().ConnectionString;
                    var dbPath = connectionString
                        .Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("data source=", "", StringComparison.OrdinalIgnoreCase)
                        .Trim();
                    if (File.Exists(dbPath))
                    {
                        logger.LogWarning("Eski SQLite veritabanı migration tablosu olmadan bulundu. Yeniden oluşturuluyor: {DbPath}", dbPath);
                        File.Delete(dbPath);
                    }
                }

                db.Database.Migrate();
            }

            await Services.GetRequiredService<ILocalizationService>().InitializeAsync();
            await Services.GetRequiredService<IApiBaseUrlProvider>().ReloadAsync();

            LoginWindow login;
            try
            {
                login = Services.GetRequiredService<LoginWindow>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LoginWindow oluşturulamadı.");
                MessageBox.Show(
                    $"Pencere oluşturulamadı.\n{ex.Message}",
                    "Obserway Label Flow",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            login.Visibility = Visibility.Visible;
            login.WindowState = WindowState.Normal;
            login.Show();
            login.UpdateLayout();
            login.Activate();
            _ = login.Focus();

            _ = FinishStartupNavigationAsync(login);
        }
        catch (Exception ex)
        {
            var loggerFactory = LoggerFactory.Create(b => b.AddDebug());
            var logger = loggerFactory.CreateLogger<App>();
            logger.LogError(ex, "Uygulama başlatma hatası.");

            MessageBox.Show(
                $"Uygulama başlatılamadı.\n{ex.Message}",
                "Obserway Label Flow",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private async Task FinishStartupNavigationAsync(LoginWindow loginWindow)
    {
        var logger = Services.GetRequiredService<ILogger<App>>();
        try
        {
            var configuration = Services.GetRequiredService<IConfiguration>();
            var timeoutSec = configuration.GetValue("Session:StartupRestoreTimeoutSeconds", 15);
            timeoutSec = Math.Clamp(timeoutSec, 3, 90);

            var session = Services.GetRequiredService<ISessionService>();
            bool ok;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
                ok = await session.RestoreOrRefreshAsync(cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                ok = false;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Oturum geri yükleme sırasında hata oluştu.");
                ok = false;
            }

            await loginWindow.Dispatcher.InvokeAsync(() =>
            {
                if (ok)
                {
                    loginWindow.Hide();
                    var main = Services.GetRequiredService<MainWindow>();
                    MainWindow = main;
                    main.Show();
                    main.Activate();
                    _ = main.Focus();
                    loginWindow.Close();
                }
                else
                {
                    loginWindow.Activate();
                    _ = loginWindow.Focus();
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Başlangıç oturum yönetimi hatası.");
            await loginWindow.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    loginWindow,
                    ex.Message,
                    "Obserway Label Flow",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                loginWindow.Activate();
                _ = loginWindow.Focus();
            });
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(2));
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            var loggerFactory = LoggerFactory.Create(b => b.AddDebug());
            loggerFactory.CreateLogger<App>().LogError(ex, "Uygulama kapanış hatası.");
        }

        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        LogException(ex, "AppDomain.UnhandledException");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "DispatcherUnhandledException");
        e.Handled = true;
    }

    private static void LogException(Exception? ex, string source)
    {
        try
        {
            using var loggerFactory = LoggerFactory.Create(b => b.AddDebug());
            var logger = loggerFactory.CreateLogger<App>();
            logger.LogError(ex, "İşlenmeyen istisna kaynağı: {Source}", source);
        }
        catch
        {
            // Son çare: hiçbir yere loglayamıyoruz.
        }
    }
}
