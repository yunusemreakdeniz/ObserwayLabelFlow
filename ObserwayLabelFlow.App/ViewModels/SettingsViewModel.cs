using System.Collections.ObjectModel;
using System.Printing;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.App.Services;

namespace ObserwayLabelFlow.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IUserSettingsStore _userSettings;
    private readonly IConfiguration _configuration;
    private readonly ILocalizationService _localization;
    private readonly IToastService _toasts;
    private readonly ILogger<SettingsViewModel> _logger;

    public Window? OwnerWindow { get; set; }

    public SettingsViewModel(IUserSettingsStore userSettings, IConfiguration configuration, ILocalizationService localization, IToastService toasts, ILogger<SettingsViewModel> logger)
    {
        _userSettings = userSettings;
        _configuration = configuration;
        _localization = localization;
        _toasts = toasts;
        _logger = logger;

        foreach (PrintQueue printer in new LocalPrintServer().GetPrintQueues())
            InstalledPrinters.Add(printer.Name);

        foreach (var mode in Enum.GetValues<BarcodeMode>())
            BarcodeModes.Add(mode);

        foreach (var orientation in Enum.GetValues<LabelOrientation>())
            Orientations.Add(orientation);

        foreach (var dpi in new[] { 200, 300, 600 })
            DpiOptions.Add(dpi);

        PaperSizePresets.Add("4x6 inch");
        PaperSizePresets.Add("10x15 cm");
        PaperSizePresets.Add("10x10 cm");
        PaperSizePresets.Add("Custom");
    }

    public async Task EnsureLoadedAsync()
    {
        if (SettingsLoaded)
            return;

        await LoadAsync();
    }

    public ObservableCollection<string> InstalledPrinters { get; } = new();

    [ObservableProperty]
    private string selectedPrinterName = string.Empty;

    [ObservableProperty]
    private string apiBaseUrl = string.Empty;

    [ObservableProperty]
    private int barcodeTimeoutMs = 250;

    [ObservableProperty]
    private BarcodeMode selectedBarcodeMode = BarcodeMode.AutoQueryPrint;

    public ObservableCollection<BarcodeMode> BarcodeModes { get; } = new();

    [ObservableProperty]
    private string selectedPaperSizePreset = "Custom";

    public ObservableCollection<string> PaperSizePresets { get; } = new();

    [ObservableProperty]
    private double paperWidthMm = 100;

    [ObservableProperty]
    private double paperHeightMm = 150;

    [ObservableProperty]
    private LabelOrientation selectedOrientation = LabelOrientation.Portrait;

    public ObservableCollection<LabelOrientation> Orientations { get; } = new();

    [ObservableProperty]
    private int selectedDpi = 300;

    public ObservableCollection<int> DpiOptions { get; } = new();

    [ObservableProperty]
    private int copies = 1;

    [ObservableProperty]
    private bool autoPrintOnQuery;

    [ObservableProperty]
    private bool clearTrackingAfterScan = true;

    [ObservableProperty]
    private string printerStatusText = string.Empty;

    [ObservableProperty]
    private string scannerStatusText = string.Empty;

    [ObservableProperty]
    private double topMarginMm;

    [ObservableProperty]
    private double bottomMarginMm;

    [ObservableProperty]
    private double leftMarginMm;

    [ObservableProperty]
    private double rightMarginMm;

    [ObservableProperty]
    private string barcodeTestInput = string.Empty;

    [ObservableProperty]
    private string barcodeTestResult = string.Empty;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    private bool settingsLoaded;

    private string _storedUiCulture = string.Empty;
    private int _loadToken;

    public event Action? CloseRequested;

    public bool WasSaved { get; private set; }

    public void ResetSession()
    {
        WasSaved = false;
        IsSaving = false;
        SettingsLoaded = false;
        Interlocked.Increment(ref _loadToken);
    }

    partial void OnSelectedPrinterNameChanged(string value) => RefreshDeviceStatus();

    partial void OnBarcodeTestInputChanged(string value)
    {
        ScannerStatusText = string.IsNullOrWhiteSpace(BarcodeTestInput)
            ? _localization.Get("StatusScannerIdle")
            : _localization.Get("StatusScannerActive");
    }

    private void RefreshDeviceStatus()
    {
        if (string.IsNullOrWhiteSpace(SelectedPrinterName))
        {
            PrinterStatusText = _localization.Get("StatusPrinterNotSet");
        }
        else
        {
            try
            {
                using var printServer = new LocalPrintServer();
                var queue = printServer.GetPrintQueues()
                    .FirstOrDefault(q => q.Name.Equals(SelectedPrinterName, StringComparison.OrdinalIgnoreCase));

                PrinterStatusText = queue is null || queue.IsOffline
                    ? _localization.Get("StatusPrinterOffline", SelectedPrinterName)
                    : _localization.Get("StatusPrinterReady", SelectedPrinterName);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ayarlar yazıcı durumu okunamadı.");
                PrinterStatusText = _localization.Get("StatusPrinterOffline", SelectedPrinterName);
            }
        }

        ScannerStatusText = string.IsNullOrWhiteSpace(BarcodeTestInput)
            ? _localization.Get("StatusScannerIdle")
            : _localization.Get("StatusScannerActive");
    }

    partial void OnSelectedPaperSizePresetChanged(string value)
    {
        switch (value)
        {
            case "4x6 inch":
                PaperWidthMm = 101.6;
                PaperHeightMm = 152.4;
                break;
            case "10x15 cm":
                PaperWidthMm = 100;
                PaperHeightMm = 150;
                break;
            case "10x10 cm":
                PaperWidthMm = 100;
                PaperHeightMm = 100;
                break;
        }
    }

    private async Task LoadAsync()
    {
        var token = Interlocked.Increment(ref _loadToken);

        try
        {
            var settings = await _userSettings.LoadAsync();
            if (token != _loadToken)
                return;

            await RunOnUiAsync(() =>
            {
                if (token != _loadToken)
                    return;

                ApplySettings(settings);
                SettingsLoaded = true;
            });
        }
        catch (Exception ex)
        {
            if (token != _loadToken)
                return;

            _logger.LogWarning(ex, "Ayarlar yüklenirken hata oluştu.");

            await RunOnUiAsync(() =>
            {
                if (token != _loadToken)
                    return;

                ApiBaseUrl = _configuration["Api:BaseUrl"] ?? string.Empty;
                BarcodeTimeoutMs = 250;
                SettingsLoaded = true;
            });
        }
    }

    private void ApplySettings(UserAppSettings settings)
    {
        _storedUiCulture = settings.UiCulture;
        SelectedPrinterName = settings.PrinterName;
        ApiBaseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
            ? _configuration["Api:BaseUrl"] ?? string.Empty
            : settings.ApiBaseUrl;
        BarcodeTimeoutMs = settings.BarcodeTimeoutMs > 0 ? settings.BarcodeTimeoutMs : 250;
        SelectedBarcodeMode = settings.BarcodeMode;

        var label = settings.LabelPrintSettings ?? new LabelPrintSettings();
        PaperWidthMm = label.PaperWidthMm;
        PaperHeightMm = label.PaperHeightMm;
        SelectedOrientation = label.Orientation;
        SelectedDpi = label.Dpi is 200 or 300 or 600 ? label.Dpi : 300;
        Copies = Math.Clamp(label.Copies, 1, 99);
        TopMarginMm = label.TopMarginMm;
        BottomMarginMm = label.BottomMarginMm;
        LeftMarginMm = label.LeftMarginMm;
        RightMarginMm = label.RightMarginMm;
        AutoPrintOnQuery = settings.AutoPrintOnQuery;
        ClearTrackingAfterScan = settings.ClearTrackingAfterScan;

        SelectedPaperSizePreset = DetectPreset(PaperWidthMm, PaperHeightMm);
        RefreshDeviceStatus();
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("UI dispatcher bulunamadı.");

        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;
    }

    private static string DetectPreset(double width, double height)
    {
        if (Math.Abs(width - 101.6) < 0.1 && Math.Abs(height - 152.4) < 0.1)
            return "4x6 inch";
        if (Math.Abs(width - 100) < 0.1 && Math.Abs(height - 150) < 0.1)
            return "10x15 cm";
        if (Math.Abs(width - 100) < 0.1 && Math.Abs(height - 100) < 0.1)
            return "10x10 cm";
        return "Custom";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await TrySaveAsync();
    }

    public async Task<bool> TrySaveAsync()
    {
        if (IsSaving)
            return false;

        if (!SettingsLoaded)
        {
            await EnsureLoadedAsync();
            if (!SettingsLoaded)
            {
                ShowSaveError();
                return false;
            }
        }

        IsSaving = true;
        try
        {
            CommitPendingBindings();

            var settings = BuildSettingsFromUi();
            await _userSettings.SaveAsync(settings);
            _storedUiCulture = settings.UiCulture;
            WasSaved = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ayarlar kaydedilirken hata oluştu.");
            ShowSaveError();
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ShowSaveError()
    {
        _toasts.Show(
            ToastKind.Error,
            _localization.Get("SettingsTitle"),
            _localization.Get("SettingsSaveFailed"),
            OwnerWindow);
    }

    private void CommitPendingBindings()
    {
        if (OwnerWindow is null)
            return;

        OwnerWindow.Dispatcher.Invoke(() =>
        {
            Keyboard.ClearFocus();
            OwnerWindow.UpdateLayout();
        });
    }

    private UserAppSettings BuildSettingsFromUi()
    {
        return new UserAppSettings
        {
            UiCulture = _storedUiCulture,
            PrinterName = SelectedPrinterName ?? string.Empty,
            ApiBaseUrl = ApiBaseUrl?.Trim() ?? string.Empty,
            BarcodeTimeoutMs = BarcodeTimeoutMs > 0 ? BarcodeTimeoutMs : 250,
            BarcodeMode = SelectedBarcodeMode,
            AutoPrintOnQuery = AutoPrintOnQuery,
            ClearTrackingAfterScan = ClearTrackingAfterScan,
            LabelPrintSettings = new LabelPrintSettings
            {
                PrinterName = SelectedPrinterName ?? string.Empty,
                PaperWidthMm = PaperWidthMm,
                PaperHeightMm = PaperHeightMm,
                Orientation = SelectedOrientation,
                Dpi = SelectedDpi,
                Copies = Math.Clamp(Copies, 1, 99),
                TopMarginMm = TopMarginMm,
                BottomMarginMm = BottomMarginMm,
                LeftMarginMm = LeftMarginMm,
                RightMarginMm = RightMarginMm
            }
        };
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand]
    private void TestBarcode()
    {
        BarcodeTestResult = string.IsNullOrWhiteSpace(BarcodeTestInput)
            ? _localization.Get("SettingsTestEmpty")
            : _localization.Get("SettingsTestRead", BarcodeTestInput.Trim());
    }
}
