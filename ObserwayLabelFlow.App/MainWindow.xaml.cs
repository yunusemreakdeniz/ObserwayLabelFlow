using System;
using System.IO;
using System.Linq;
using System.Printing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.App.Services;
using ObserwayLabelFlow.App.ViewModels;
using ObserwayLabelFlow.App.Views;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.Security;

namespace ObserwayLabelFlow.App;

public partial class MainWindow : Window
{
    private readonly ISessionService _sessionService;
    private readonly IConfiguration _configuration;
    private readonly ILocalizationService _localization;
    private readonly IUserSettingsStore _userSettings;
    private readonly ILabelPdfLoader _labelPdfLoader;
    private readonly ILabelPrintService _labelPrintService;
    private readonly IApiBaseUrlProvider _apiBaseUrl;
    private readonly IAppDialogService _dialogs;
    private readonly IToastService _toasts;
    private readonly ILogger<MainWindow> _logger;
    private DispatcherTimer? _sessionTimer;
    private DispatcherTimer? _barcodeTimer;
    private DispatcherTimer? _statusTimer;
    private bool _sessionTickRunning;
    private bool _printPending;
    private bool _isPrinting;
    private DateTimeOffset _lastBarcodeInputUtc = DateTimeOffset.MinValue;
    private string? _lastProcessedTracking;
    private int _labelPreviewGeneration;
    private int _statusTickCount;
    private string? _localLabelFilePath;

    public MainWindow(MainViewModel vm, ISessionService sessionService, IConfiguration configuration, ILocalizationService localization, IUserSettingsStore userSettings, ILabelPdfLoader labelPdfLoader, ILabelPrintService labelPrintService, IApiBaseUrlProvider apiBaseUrl, IAppDialogService dialogs, IToastService toasts, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        DataContext = vm;
        vm.LogoutRequested += OnLogoutRequested;
        vm.PrintRequested += OnPrintRequested;
        Closed += (_, _) =>
        {
            vm.LogoutRequested -= OnLogoutRequested;
            vm.PrintRequested -= OnPrintRequested;
        };
        _sessionService = sessionService;
        _configuration = configuration;
        _localization = localization;
        _userSettings = userSettings;
        _labelPdfLoader = labelPdfLoader;
        _labelPrintService = labelPrintService;
        _apiBaseUrl = apiBaseUrl;
        _dialogs = dialogs;
        _toasts = toasts;
        _logger = logger;
        Closed += (_, _) =>
        {
            _sessionTimer?.Stop();
            _barcodeTimer?.Stop();
            _statusTimer?.Stop();
        };
    }

    private void ShowPdfPreview(string filePath)
    {
        PdfPreviewImage.Source = LabelPdfPreviewRenderer.RenderFirstPage(filePath);
    }

    private void ClearPdfPreview()
    {
        PdfPreviewImage.Source = null;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            Window.GetWindow(fe)?.Close();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            var w = Window.GetWindow(fe);
            if (w is not null)
                w.WindowState = WindowState.Minimized;
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
            return;

        var settingsWindow = app.Services.GetRequiredService<SettingsWindow>();
        if (settingsWindow.DataContext is not SettingsViewModel svm)
            return;

        svm.ResetSession();

        try
        {
            await svm.EnsureLoadedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ayarlar yüklenemedi.");
            _dialogs.Show(AppDialogKind.Error, _localization.Get("SettingsTitle"), _localization.Get("SettingsSaveFailed"), this);
            return;
        }

        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();

        await ApplySettingsAfterSaveAsync(svm);

        if (svm.WasSaved)
        {
            _toasts.Show(
                ToastKind.Success,
                _localization.Get("SettingsTitle"),
                _localization.Get("SettingsSaved"),
                this);
        }

        TryFocusTrackingBox();
    }

    private async Task ApplySettingsAfterSaveAsync(SettingsViewModel svm)
    {
        if (!svm.WasSaved || DataContext is not MainViewModel vm)
            return;

        try
        {
            await _apiBaseUrl.ReloadAsync();
            await vm.ReloadSettingsFromStoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ayarlar kapatılırken durum güncellenemedi.");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d)
        {
            if (FindAncestor<ComboBox>(d) is not null)
                return;
            if (FindAncestor<Button>(d) is not null)
                return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var cur = start;
        while (cur is not null)
        {
            if (cur is T typed)
                return typed;
            cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
        }
        return null;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedHistoryProvider = () =>
                    vm.History.Where(x => x.IsSelected).ToList();

                vm.PromptSaveExcelFile = suggested =>
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = suggested,
                        Filter = "Excel (*.xlsx)|*.xlsx",
                        DefaultExt = ".xlsx",
                        AddExtension = true
                    };
                    return dlg.ShowDialog(this) == true ? dlg.FileName : null;
                };

                await vm.InitializeAsync();
                vm.PropertyChanged += ViewModelOnPropertyChanged;
                vm.LabelPreviewChanged += OnLabelPreviewChanged;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ana pencere başlatma hatası.");
        }

        Activated += Window_Activated;

        FocusTrackingBoxIfOperationTab();

        var sec = _configuration.GetValue("Session:RefreshCheckSeconds", 60);
        sec = Math.Clamp(sec, 15, 3600);
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(sec) };
        _sessionTimer.Tick += SessionTimerOnTick;
        _sessionTimer.Start();

        _barcodeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _barcodeTimer.Tick += BarcodeTimerOnTick;
        _barcodeTimer.Start();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += StatusTimerOnTick;
        _statusTimer.Start();
    }

    private void OnLabelPreviewChanged(Uri? source)
    {
        if (source is null)
            _ = ClearLabelPreviewAsync();
        else
            _ = LoadLabelPreviewAsync(source);
    }

    private async Task LoadLabelPreviewAsync(Uri source)
    {
        var generation = Interlocked.Increment(ref _labelPreviewGeneration);

        try
        {
            var preview = await _labelPdfLoader.PreparePreviewAsync(source);
            if (generation != _labelPreviewGeneration)
                return;

            _localLabelFilePath = preview.LocalFilePath;
            await Dispatcher.InvokeAsync(() => ShowPdfPreview(preview.LocalFilePath));

            if (_printPending)
                await TryPrintWhenReadyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Etiket önizlemesi yüklenemedi. Url={Url}", source);
            _localLabelFilePath = null;
            await Dispatcher.InvokeAsync(ClearPdfPreview);
            _toasts.Show(
                ToastKind.Error,
                _localization.Get("LabelReadyTitle"),
                _localization.Get("LabelPreviewLoadFailed"),
                this);
        }
    }

    private Task ClearLabelPreviewAsync()
    {
        var generation = Interlocked.Increment(ref _labelPreviewGeneration);
        _printPending = false;
        _localLabelFilePath = null;

        if (generation == _labelPreviewGeneration)
            ClearPdfPreview();

        ScheduleTrackingFocus();
        return Task.CompletedTask;
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        if (ShouldRefocusTrackingFromCurrentFocus())
            ScheduleTrackingFocus();
    }

    private void ViewModelOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (e.PropertyName == nameof(MainViewModel.SelectedTabIndex) && vm.SelectedTabIndex == 0)
            ScheduleTrackingFocus();

        if (e.PropertyName == nameof(MainViewModel.IsBusy) && !vm.IsBusy)
            ScheduleTrackingFocus();
    }

    private void ScheduleTrackingFocus()
        => Dispatcher.BeginInvoke(FocusTrackingBoxIfOperationTab, DispatcherPriority.ApplicationIdle);

    private bool ShouldRefocusTrackingFromCurrentFocus()
    {
        if (!ShouldKeepTrackingFocus())
            return false;

        if (TrackingBox.IsKeyboardFocused)
            return false;

        var focused = Keyboard.FocusedElement;
        if (focused is null or Window)
            return true;

        if (focused is not DependencyObject element)
            return false;

        if (FindAncestor<Button>(element) is not null)
            return false;

        if (FindAncestor<DataGrid>(element) is not null)
            return false;

        if (FindAncestor<DatePicker>(element) is not null)
            return false;

        if (FindAncestor<ComboBox>(element) is not null)
            return false;

        if (FindAncestor<CheckBox>(element) is not null)
            return false;

        return false;
    }

    private void TryFocusTrackingBox() => ScheduleTrackingFocus();

    private void FocusTrackingBoxIfOperationTab()
    {
        if (!ShouldKeepTrackingFocus())
            return;

        if (TrackingBox.IsKeyboardFocused)
            return;

        TrackingBox.Focus();
        Keyboard.Focus(TrackingBox);
        TrackingBox.CaretIndex = TrackingBox.Text.Length;
    }

    private bool ShouldKeepTrackingFocus()
    {
        if (DataContext is not MainViewModel vm)
            return false;

        if (vm.SelectedTabIndex != 0)
            return false;

        if (!IsActive || !IsVisible)
            return false;

        if (vm.IsUserMenuOpen)
            return false;

        return !HasBlockingOwnedDialogOpen();
    }

    private bool HasBlockingOwnedDialogOpen()
        => OwnedWindows.Cast<Window>().Any(w => w.IsVisible);

    private void TrackingBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _lastBarcodeInputUtc = DateTimeOffset.UtcNow;
        if (DataContext is MainViewModel vm)
        {
            vm.RefreshScannerStatus(_lastBarcodeInputUtc);
            if (string.IsNullOrWhiteSpace(vm.TrackingNumber))
                _lastProcessedTracking = null;
        }
    }

    private void TrackingBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Return)
            return;

        e.Handled = true;
        if (DataContext is not MainViewModel vm)
            return;

        _lastProcessedTracking = vm.TrackingNumber?.Trim();
        _ = ExecuteBarcodeActionAsync(vm);
    }

    private async void BarcodeTimerOnTick(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var tn = vm.TrackingNumber?.Trim();

        if (string.IsNullOrWhiteSpace(tn))
        {
            _lastProcessedTracking = null;
            return;
        }

        if (tn == _lastProcessedTracking)
            return;

        var timeout = vm.BarcodeTimeoutMs > 0 ? vm.BarcodeTimeoutMs : 250;
        if ((DateTimeOffset.UtcNow - _lastBarcodeInputUtc).TotalMilliseconds < timeout)
            return;

        if (tn.Length < 3)
            return;

        _lastProcessedTracking = tn;
        await ExecuteBarcodeActionAsync(vm);
    }

    private async Task ExecuteBarcodeActionAsync(MainViewModel vm)
    {
        try
        {
            if (vm.BarcodeMode == BarcodeMode.AutoQueryPrint)
                await vm.QueryAndPrintAsync();
            else
                await vm.QueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Barkod tetiklemeli sorgu/yazdırma hatası. Tracking={TrackingNumber}", vm.TrackingNumber);
            _dialogs.Show(AppDialogKind.Error, _localization.Get("Query_NotFoundTitle"), _localization.Get("Error_Connection"), this);
        }
        finally
        {
            vm.ClearTrackingForNextScan();
            _lastProcessedTracking = null;
            ScheduleTrackingFocus();
        }
    }

    private void StatusTimerOnTick(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.RefreshScannerStatus(_lastBarcodeInputUtc);

        _statusTickCount++;
        if (_statusTickCount % 5 == 0)
            _ = RefreshPrinterStatusAsync(vm);
    }

    private async Task RefreshPrinterStatusAsync(MainViewModel vm)
    {
        try
        {
            var settings = await _userSettings.LoadAsync();
            vm.RefreshPrinterStatus(settings.PrinterName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Periyodik yazıcı durumu güncellenemedi.");
        }
    }

    private async void SessionTimerOnTick(object? sender, EventArgs e)
    {
        if (_sessionTickRunning)
            return;
        _sessionTickRunning = true;
        _sessionTimer?.Stop();
        var reopenLogin = false;
        try
        {
            reopenLogin = !await _sessionService.TryMaintainSessionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Oturum yenileme kontrolü sırasında hata oluştu.");
            reopenLogin = true;
        }
        finally
        {
            _sessionTickRunning = false;
            if (reopenLogin)
                OpenLoginAndClose();
            else
                _sessionTimer?.Start();
        }
    }

    private void OnLogoutRequested() => OpenLoginAndClose();

    private void OpenLoginAndClose()
    {
        Dispatcher.Invoke(() =>
        {
            _sessionTimer?.Stop();
            _sessionTimer = null;
            _barcodeTimer?.Stop();
            _barcodeTimer = null;
            _statusTimer?.Stop();
            _statusTimer = null;
            if (Application.Current is App app)
            {
                var login = app.Services.GetRequiredService<LoginWindow>();
                login.Show();
            }
            Close();
        });
    }

    private void OnPrintRequested()
    {
        _printPending = true;
        _ = TryPrintWhenReadyAsync();
    }

    private async Task TryPrintWhenReadyAsync()
    {
        if (!_printPending || _isPrinting)
            return;

        try
        {
            if (DataContext is not MainViewModel vm || vm.PdfSource is null)
                return;

            if (string.IsNullOrWhiteSpace(_localLabelFilePath) || !File.Exists(_localLabelFilePath))
                await LoadLabelPreviewAsync(vm.PdfSource);

            if (string.IsNullOrWhiteSpace(_localLabelFilePath) || !File.Exists(_localLabelFilePath))
                return;

            _isPrinting = true;
            _printPending = false;
            await PrintPdfAsync(_localLabelFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yazdırma hazırlığı sırasında hata oluştu.");
            _printPending = false;
        }
        finally
        {
            _isPrinting = false;
        }
    }

    private async Task PrintPdfAsync(string pdfPath)
    {
        try
        {
            var settings = await _userSettings.LoadAsync();
            var labelSettings = settings.LabelPrintSettings ?? new LabelPrintSettings();
            var printerName = labelSettings.PrinterName;

            if (!string.IsNullOrWhiteSpace(printerName))
            {
                using var printServer = new LocalPrintServer();
                var exists = printServer.GetPrintQueues()
                    .Any(q => q.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                    printerName = string.Empty;
            }

            var printSettings = new LabelPrintSettings
            {
                PrinterName = printerName,
                PaperWidthMm = labelSettings.PaperWidthMm,
                PaperHeightMm = labelSettings.PaperHeightMm,
                Orientation = labelSettings.Orientation,
                Dpi = labelSettings.Dpi,
                Copies = labelSettings.Copies,
                TopMarginMm = labelSettings.TopMarginMm,
                BottomMarginMm = labelSettings.BottomMarginMm,
                LeftMarginMm = labelSettings.LeftMarginMm,
                RightMarginMm = labelSettings.RightMarginMm
            };

            var success = await _labelPrintService.PrintAsync(pdfPath, printSettings);

            if (DataContext is MainViewModel vm)
            {
                await vm.UpdateLastPrintResultAsync(success, success ? null : "PrintFailed");
                vm.RefreshPrinterStatus(settings.PrinterName);
            }

            if (!success)
            {
                _dialogs.Show(
                    AppDialogKind.Warning,
                    _localization.Get("Print_Title"),
                    _localization.Get("Print_StatusFailed", "PrintFailed"),
                    this);
            }

            TryFocusTrackingBox();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yazdırma sırasında hata oluştu.");

            if (DataContext is MainViewModel vm)
                await vm.UpdateLastPrintResultAsync(false, ex.Message);

            _dialogs.Show(
                AppDialogKind.Error,
                _localization.Get("Print_Title"),
                $"{_localization.Get("Print_StartFailed")}\n{ex.Message}",
                this);

            TryFocusTrackingBox();
        }
    }
}
