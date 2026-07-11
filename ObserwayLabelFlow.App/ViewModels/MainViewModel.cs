using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.App.Services;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.History;
using ObserwayLabelFlow.Core.Orders;
using ObserwayLabelFlow.Core.Security;

namespace ObserwayLabelFlow.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITokenStore _tokenStore;
    private readonly IHistoryService _history;
    private readonly ILocalizationService _localization;
    private readonly IOrdersApiClient _ordersApiClient;
    private readonly IUserSettingsStore _userSettings;
    private readonly IApiBaseUrlProvider _apiBaseUrl;
    private readonly IAppDialogService _dialogs;
    private readonly IHistoryExportService _historyExport;
    private readonly ILogger<MainViewModel> _logger;

    private string? _welcomeDisplayName;
    private string? _lastQueriedTracking;
    private bool _productSummaryIsDefaultHint = true;
    private bool _suppressCultureSelection;
    private UserAppSettings? _currentSettings;
    private PrintHistoryEntry? _lastHistoryEntry;

    public MainViewModel(ITokenStore tokenStore, IHistoryService history, ILocalizationService localization, IOrdersApiClient ordersApiClient, IUserSettingsStore userSettings, IApiBaseUrlProvider apiBaseUrl, IAppDialogService dialogs, IHistoryExportService historyExport, ILogger<MainViewModel> logger)
    {
        _tokenStore = tokenStore;
        _history = history;
        _localization = localization;
        _ordersApiClient = ordersApiClient;
        _userSettings = userSettings;
        _apiBaseUrl = apiBaseUrl;
        _dialogs = dialogs;
        _historyExport = historyExport;
        _logger = logger;
        ProductSummary = _localization.Get("ProductSummaryHint");
        UserDisplayName = _localization.Get("UserGuest");
        _localization.CultureChanged += OnLocalizationCultureChanged;
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
        ApplyUserDisplayName();
        if (_lastQueriedTracking is not null)
            ProductSummary = _localization.Get("ProductSummaryAfterQuery", _lastQueriedTracking);
        else if (_productSummaryIsDefaultHint)
            ProductSummary = _localization.Get("ProductSummaryHint");
    }

    public event Action? LogoutRequested;
    public event Action? PrintRequested;

    [ObservableProperty]
    private string userDisplayName = string.Empty;

    [ObservableProperty]
    private bool isUserMenuOpen;

    [ObservableProperty]
    private string trackingNumber = string.Empty;

    partial void OnTrackingNumberChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            QueryAndPrintCommand.NotifyCanExecuteChanged();
            return;
        }

        var cleaned = value.Trim().Trim('\r', '\n', '\t');
        if (cleaned != value)
            TrackingNumber = cleaned;

        QueryAndPrintCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private string productSummary = string.Empty;

    [ObservableProperty]
    private string currentOrderNumber = string.Empty;

    [ObservableProperty]
    private string currentAmazonOrderId = string.Empty;

    [ObservableProperty]
    private string currentCustomerName = string.Empty;

    [ObservableProperty]
    private string currentCarrierName = string.Empty;

    [ObservableProperty]
    private string currentCarrierService = string.Empty;

    [ObservableProperty]
    private string currentOrderStatus = string.Empty;

    [ObservableProperty]
    private string displayedTrackingNumber = string.Empty;

    [ObservableProperty]
    private bool labelReady;

    [ObservableProperty]
    private bool autoPrintOnQuery = true;

    [ObservableProperty]
    private bool clearTrackingAfterScan = true;

    public ObservableCollection<ProductPreviewItem> ProductItems { get; } = new();

    [ObservableProperty]
    private Uri? pdfSource;

    [ObservableProperty]
    private string pdfUrl = string.Empty;

    partial void OnPdfSourceChanged(Uri? value)
    {
        PdfUrl = value?.ToString() ?? string.Empty;
        LabelReady = value is not null;
        PrintLabelCommand.NotifyCanExecuteChanged();
        LabelPreviewChanged?.Invoke(value);
    }

    public event Action<Uri?>? LabelPreviewChanged;

    [ObservableProperty]
    private bool isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        QueryAndPrintCommand.NotifyCanExecuteChanged();
        PrintLabelCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private bool hasActiveOrder;

    [ObservableProperty]
    private bool isHistoryEmpty = true;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private string printerStatusText = string.Empty;

    [ObservableProperty]
    private Brush printerStatusBrush = Brushes.Gray;

    [ObservableProperty]
    private string scannerStatusText = string.Empty;

    [ObservableProperty]
    private Brush scannerStatusBrush = Brushes.Gray;

    [ObservableProperty]
    private int barcodeTimeoutMs = 250;

    [ObservableProperty]
    private BarcodeMode barcodeMode = BarcodeMode.AutoQueryPrint;

    [ObservableProperty]
    private string historySearchText = string.Empty;

    [ObservableProperty]
    private DateTime? historyFromDate;

    [ObservableProperty]
    private DateTime? historyToDate;

    [ObservableProperty]
    private bool historyOnlyErrors;

    [ObservableProperty]
    private bool isHistoryAdvancedFilterOpen;

    [ObservableProperty]
    private bool? allHistorySelected;

    [ObservableProperty]
    private int selectedHistoryCount;

    [ObservableProperty]
    private int productItemCount;

    public ObservableCollection<PrintHistoryEntry> History { get; } = new();

    private bool _syncingHistorySelection;
    private bool _clearingHistoryFilter;

    public Func<IReadOnlyList<PrintHistoryEntry>>? SelectedHistoryProvider { get; set; }

    public Func<string, string?>? PromptSaveExcelFile { get; set; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            var session = await _tokenStore.GetAsync(ct);
            _welcomeDisplayName = session?.DisplayName;
            ApplyUserDisplayName();

            var idx = LanguageOptions.Select((x, i) => new { x.Code, Index = i })
                .FirstOrDefault(x => x.Code == _localization.CurrentCultureName)?.Index ?? -1;
            _suppressCultureSelection = true;
            SelectedLanguageIndex = idx;
            _suppressCultureSelection = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Başlangıç token okuma hatası.");
        }

        await LoadSettingsAsync(ct);
        await RefreshHistoryAsync(ct);
    }

    public async Task ReloadSettingsFromStoreAsync(CancellationToken ct = default)
    {
        await LoadSettingsAsync(ct);
    }

    private async Task LoadSettingsAsync(CancellationToken ct)
    {
        try
        {
            _currentSettings = await _userSettings.LoadAsync(ct);
            BarcodeTimeoutMs = _currentSettings.BarcodeTimeoutMs > 0 ? _currentSettings.BarcodeTimeoutMs : 250;
            BarcodeMode = _currentSettings.BarcodeMode;
            AutoPrintOnQuery = _currentSettings.AutoPrintOnQuery;
            ClearTrackingAfterScan = _currentSettings.ClearTrackingAfterScan;
            RefreshPrinterStatus(_currentSettings.PrinterName);
            RefreshScannerStatus(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kullanıcı ayarları yüklenirken hata oluştu.");
            _currentSettings = new UserAppSettings();
            BarcodeTimeoutMs = 250;
            BarcodeMode = BarcodeMode.AutoQueryPrint;
            RefreshPrinterStatus(string.Empty);
            RefreshScannerStatus(null);
        }
    }

    public void RefreshPrinterStatus(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            PrinterStatusText = _localization.Get("StatusPrinterNotSet");
            PrinterStatusBrush = new SolidColorBrush(Color.FromRgb(0xB6, 0xC2, 0xE2));
            return;
        }

        try
        {
            using var printServer = new System.Printing.LocalPrintServer();
            var queue = printServer.GetPrintQueues()
                .FirstOrDefault(q => q.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase));

            if (queue is null)
            {
                PrinterStatusText = _localization.Get("StatusPrinterOffline", printerName);
                PrinterStatusBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
                return;
            }

            if (queue.IsOffline)
            {
                PrinterStatusText = _localization.Get("StatusPrinterOffline", printerName);
                PrinterStatusBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            }
            else
            {
                PrinterStatusText = _localization.Get("StatusPrinterReady", printerName);
                PrinterStatusBrush = new SolidColorBrush(Color.FromRgb(0x6E, 0xA8, 0xFE));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yazıcı durumu kontrol edilirken hata oluştu.");
            PrinterStatusText = _localization.Get("StatusPrinterOffline", printerName);
            PrinterStatusBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
        }
    }

    public void RefreshScannerStatus(DateTimeOffset? lastScanUtc)
    {
        var active = lastScanUtc.HasValue && (DateTimeOffset.UtcNow - lastScanUtc.Value).TotalSeconds < 30;
        ScannerStatusText = active
            ? _localization.Get("StatusScannerActive")
            : _localization.Get("StatusScannerIdle");
        ScannerStatusBrush = active
            ? new SolidColorBrush(Color.FromRgb(0x6E, 0xA8, 0xFE))
            : new SolidColorBrush(Color.FromRgb(0xB6, 0xC2, 0xE2));
    }

    private void ApplyUserDisplayName()
    {
        UserDisplayName = string.IsNullOrWhiteSpace(_welcomeDisplayName)
            ? _localization.Get("UserGuest")
            : _welcomeDisplayName!;
    }

    [RelayCommand]
    private void ToggleUserMenu() => IsUserMenuOpen = !IsUserMenuOpen;

    [RelayCommand]
    public async Task<bool> QueryAsync()
    {
        if (string.IsNullOrWhiteSpace(TrackingNumber))
        {
            ProductSummary = _localization.Get("ProductSummaryEmptyTracking");
            _productSummaryIsDefaultHint = false;
            ClearCurrentOrderInfo();
            ProductItems.Clear();
            _dialogs.Show(AppDialogKind.Warning, _localization.Get("Query_NotFoundTitle"), ProductSummary);
            return false;
        }

        IsBusy = true;
        SelectedTabIndex = 0;
        try
        {
            var tn = TrackingNumber.Trim();
            _lastQueriedTracking = tn;
            DisplayedTrackingNumber = tn;
            _productSummaryIsDefaultHint = false;

            var result = await _ordersApiClient.GetOrderByTrackingNumberAsync(tn, CancellationToken.None);
            if (!result.IsSuccess || result.Value is null)
            {
                ProductItems.Clear();
                var errorText = result.Errors.Count > 0
                    ? string.Join("\n", result.Errors)
                    : _localization.Get("Query_NotFound", tn);
                ProductSummary = errorText;
                ClearCurrentOrderInfo();
                LastQueryFailedMessage = errorText;
                LastQuerySucceeded = false;
                _dialogs.Show(AppDialogKind.Warning, _localization.Get("Query_NotFoundTitle"), errorText);
                return false;
            }

            LastQuerySucceeded = true;
            LastQueryFailedMessage = null;
            HasActiveOrder = true;

            var order = result.Value;
            ApplyOrderToUi(order, tn);

            var labelSettings = _currentSettings?.LabelPrintSettings ?? new LabelPrintSettings();
            _lastHistoryEntry = OrderPresentationMapper.CreateHistoryEntry(
                order,
                tn,
                labelSettings,
                _welcomeDisplayName,
                _localization.Get("HistoryNotesSample"));

            await _history.AddAsync(_lastHistoryEntry, CancellationToken.None);
            await RefreshHistoryAsync(CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sipariş sorgulanırken hata oluştu. Tracking={TrackingNumber}", TrackingNumber);
            ProductSummary = _localization.Get("Error_Connection");
            ClearCurrentOrderInfo();
            LastQueryFailedMessage = ProductSummary;
            LastQuerySucceeded = false;
            HasActiveOrder = false;
            _dialogs.Show(AppDialogKind.Error, _localization.Get("Query_NotFoundTitle"), ProductSummary);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [ObservableProperty]
    private bool lastQuerySucceeded;

    [ObservableProperty]
    private string? lastQueryFailedMessage;

    private async Task RefreshHistoryAsync(CancellationToken ct)
    {
        try
        {
            var rows = await _history.GetRecentAsync(200, ct);
            ReplaceHistoryRows(rows);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geçmiş yüklenirken hata oluştu.");
        }
    }

    [RelayCommand]
    private async Task ApplyHistoryFilterAsync()
    {
        try
        {
            var filter = new HistoryFilter
            {
                SearchText = HistorySearchText,
                FromDateUtc = ToUtcStartOfLocalDay(HistoryFromDate),
                ToDateUtc = ToUtcEndOfLocalDay(HistoryToDate),
                OnlyErrors = HistoryOnlyErrors,
                Take = 200
            };

            var rows = await _history.GetAsync(filter, CancellationToken.None);
            ReplaceHistoryRows(rows);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geçmiş filtrelenirken hata oluştu.");
        }
    }

    partial void OnHistoryFromDateChanged(DateTime? value)
    {
        if (!_clearingHistoryFilter)
            _ = ApplyHistoryFilterWhenReadyAsync();
    }

    partial void OnHistoryToDateChanged(DateTime? value)
    {
        if (!_clearingHistoryFilter)
            _ = ApplyHistoryFilterWhenReadyAsync();
    }

    partial void OnHistoryOnlyErrorsChanged(bool value)
    {
        if (!_clearingHistoryFilter)
            _ = ApplyHistoryFilterWhenReadyAsync();
    }

    partial void OnAllHistorySelectedChanged(bool? value)
    {
        if (_syncingHistorySelection || !value.HasValue)
            return;

        _syncingHistorySelection = true;
        try
        {
            foreach (var row in History)
                row.IsSelected = value.Value;
        }
        finally
        {
            _syncingHistorySelection = false;
        }
    }

    private async Task ApplyHistoryFilterWhenReadyAsync()
    {
        if (IsHistoryFilterActive())
            await ApplyHistoryFilterAsync();
        else
            await RefreshHistoryAsync(CancellationToken.None);
    }

    private void ReplaceHistoryRows(IEnumerable<PrintHistoryEntry> rows)
    {
        DetachHistoryRowHandlers();
        History.Clear();

        foreach (var row in rows)
        {
            AttachHistoryRowHandlers(row);
            History.Add(row);
        }

        IsHistoryEmpty = History.Count == 0;
        UpdateAllHistorySelectedState();
    }

    private void AttachHistoryRowHandlers(PrintHistoryEntry row)
        => row.PropertyChanged += OnHistoryRowSelectionChanged;

    private void DetachHistoryRowHandlers()
    {
        foreach (var row in History)
            row.PropertyChanged -= OnHistoryRowSelectionChanged;
    }

    private void OnHistoryRowSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncingHistorySelection || e.PropertyName != nameof(PrintHistoryEntry.IsSelected))
            return;

        UpdateAllHistorySelectedState();
    }

    private void UpdateAllHistorySelectedState()
    {
        _syncingHistorySelection = true;
        try
        {
            if (History.Count == 0)
            {
                AllHistorySelected = false;
                SelectedHistoryCount = 0;
                return;
            }

            var selectedCount = History.Count(x => x.IsSelected);
            SelectedHistoryCount = selectedCount;
            AllHistorySelected = selectedCount switch
            {
                0 => false,
                _ when selectedCount == History.Count => true,
                _ => null
            };
        }
        finally
        {
            _syncingHistorySelection = false;
        }
    }

    [RelayCommand]
    private void ToggleHistoryAdvancedFilter()
    {
        IsHistoryAdvancedFilterOpen = !IsHistoryAdvancedFilterOpen;
    }

    [RelayCommand]
    private async Task ClearHistoryFilterAsync()
    {
        _clearingHistoryFilter = true;
        try
        {
            HistorySearchText = string.Empty;
            HistoryFromDate = null;
            HistoryToDate = null;
            HistoryOnlyErrors = false;
            AllHistorySelected = false;
        }
        finally
        {
            _clearingHistoryFilter = false;
        }

        await RefreshHistoryAsync(CancellationToken.None);
    }

    public async Task UpdateLastPrintResultAsync(bool success, string? errorMessage = null)
    {
        if (_lastHistoryEntry is null)
            return;

        try
        {
            _lastHistoryEntry.Success = success;
            _lastHistoryEntry.ErrorMessage = errorMessage;
            await _history.UpdateAsync(_lastHistoryEntry, CancellationToken.None);
            await RefreshHistoryAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yazdırma sonucu güncellenirken hata oluştu.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanQueryAndPrint))]
    public async Task<bool> QueryAndPrintAsync()
    {
        var ok = await QueryAsync();
        if (ok && PdfSource is not null && AutoPrintOnQuery)
            RequestPrint();
        return ok;
    }

    private bool CanQueryAndPrint() => !IsBusy && !string.IsNullOrWhiteSpace(TrackingNumber);

    [RelayCommand(CanExecute = nameof(CanPrintLabel))]
    private void PrintLabel() => RequestPrint();

    private bool CanPrintLabel() => !IsBusy && PdfSource is not null;

    public void RequestPrint()
    {
        if (PdfSource is not null)
            PrintRequested?.Invoke();
    }

    public void ClearTrackingForNextScan()
    {
        TrackingNumber = string.Empty;
    }

    private void ApplyOrderToUi(OrderDto order, string scannedTrackingNumber)
    {
        var tracking = string.IsNullOrWhiteSpace(order.TrackingNumber)
            ? scannedTrackingNumber
            : order.TrackingNumber.Trim();

        DisplayedTrackingNumber = tracking;
        ProductSummary = _localization.Get("ProductSummaryAfterQuery", tracking);

        CurrentOrderNumber = order.ObserwayOrderNumber ?? string.Empty;
        CurrentAmazonOrderId = order.AmazonOrderId ?? string.Empty;
        CurrentCustomerName = order.Customer?.FullName ?? string.Empty;
        CurrentCarrierName = order.CarrierName ?? string.Empty;
        CurrentCarrierService = order.CarrierService ?? string.Empty;
        CurrentOrderStatus = order.OrderStatus ?? string.Empty;

        PdfSource = OrderPresentationMapper.TryCreateLabelUri(order.Label, _apiBaseUrl.GetBaseUrl());

        ProductItems.Clear();
        foreach (var product in order.Products)
            ProductItems.Add(OrderPresentationMapper.ToProductPreviewItem(product));

        ProductItemCount = ProductItems.Count;

        if (ProductItems.Count == 0)
            ProductItems.Add(new ProductPreviewItem { OfficialName = _localization.Get("ProductSummaryEmptyProducts") });
    }

    private void ClearCurrentOrderInfo()
    {
        PdfSource = null;
        CurrentOrderNumber = string.Empty;
        CurrentAmazonOrderId = string.Empty;
        CurrentCustomerName = string.Empty;
        CurrentCarrierName = string.Empty;
        CurrentCarrierService = string.Empty;
        CurrentOrderStatus = string.Empty;
        DisplayedTrackingNumber = string.Empty;
        LabelReady = false;
        HasActiveOrder = false;
        ProductItemCount = 0;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        IsUserMenuOpen = false;
        try
        {
            await _tokenStore.ClearAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Çıkış yapılırken token temizlenemedi.");
        }
        LogoutRequested?.Invoke();
    }

    [RelayCommand]
    private void Reprint(PrintHistoryEntry? entry)
    {
        if (entry?.PdfUrl is null)
            return;

        PdfSource = OrderPresentationMapper.TryCreateLabelUri(entry.PdfUrl, _apiBaseUrl.GetBaseUrl());
        PrintRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SelectHistoryEntryAsync(PrintHistoryEntry? entry)
    {
        if (entry is null)
            return;

        TrackingNumber = entry.TrackingNumber;
        SelectedTabIndex = 0;
        await QueryAsync();
    }

    private static DateTimeOffset? ToUtcStartOfLocalDay(DateTime? date)
    {
        if (!date.HasValue)
            return null;

        var localStart = date.Value.Date;
        var offset = TimeZoneInfo.Local.GetUtcOffset(localStart);
        return new DateTimeOffset(localStart, offset).ToUniversalTime();
    }

    private static DateTimeOffset? ToUtcEndOfLocalDay(DateTime? date)
    {
        if (!date.HasValue)
            return null;

        var localEnd = date.Value.Date.AddDays(1).AddTicks(-1);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localEnd);
        return new DateTimeOffset(localEnd, offset).ToUniversalTime();
    }

    private bool IsHistoryFilterActive()
        => !string.IsNullOrWhiteSpace(HistorySearchText)
           || HistoryFromDate.HasValue
           || HistoryToDate.HasValue
           || HistoryOnlyErrors;

    private async Task ReloadHistoryAsync(CancellationToken ct = default)
    {
        if (IsHistoryFilterActive())
            await ApplyHistoryFilterAsync();
        else
            await RefreshHistoryAsync(ct);
    }

    [RelayCommand]
    private async Task DeleteHistoryEntryAsync(PrintHistoryEntry? entry)
    {
        if (entry is null)
            return;

        if (!_dialogs.Confirm(
                _localization.Get("HistoryDeleteConfirmTitle"),
                _localization.Get("HistoryDeleteConfirmSingle"),
                Application.Current.MainWindow))
            return;

        try
        {
            await _history.DeleteAsync(entry.Id, CancellationToken.None);
            await ReloadHistoryAsync(CancellationToken.None);
            _dialogs.Show(AppDialogKind.Info, _localization.Get("TabHistory"), _localization.Get("HistoryDeleteSuccess"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geçmiş kaydı silinirken hata oluştu. Id={Id}", entry.Id);
            _dialogs.Show(AppDialogKind.Error, _localization.Get("TabHistory"), _localization.Get("HistoryDeleteFailed"));
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedHistoryAsync()
    {
        var selected = SelectedHistoryProvider?.Invoke();
        if (selected is not { Count: > 0 })
        {
            _dialogs.Show(AppDialogKind.Warning, _localization.Get("TabHistory"), _localization.Get("HistoryDeleteNone"));
            return;
        }

        if (!_dialogs.Confirm(
                _localization.Get("HistoryDeleteConfirmTitle"),
                _localization.Get("HistoryDeleteConfirmMultiple", selected.Count),
                Application.Current.MainWindow))
            return;

        try
        {
            await _history.DeleteManyAsync(selected.Select(x => x.Id), CancellationToken.None);
            await ReloadHistoryAsync(CancellationToken.None);
            _dialogs.Show(AppDialogKind.Info, _localization.Get("TabHistory"), _localization.Get("HistoryDeleteSuccessSelected", selected.Count));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Seçili geçmiş kayıtları silinirken hata oluştu.");
            _dialogs.Show(AppDialogKind.Error, _localization.Get("TabHistory"), _localization.Get("HistoryDeleteFailed"));
        }
    }

    [RelayCommand]
    private void ExportHistoryToExcel()
    {
        try
        {
            var selected = SelectedHistoryProvider?.Invoke();
            var entries = selected is { Count: > 0 }
                ? selected
                : History.ToList();

            if (entries.Count == 0)
            {
                _dialogs.Show(AppDialogKind.Warning, _localization.Get("TabHistory"), _localization.Get("HistoryExportEmpty"));
                return;
            }

            var suggestedName = $"LabelFlow_History_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            var path = PromptSaveExcelFile?.Invoke(suggestedName);
            if (string.IsNullOrWhiteSpace(path))
                return;

            _historyExport.ExportToExcel(entries, path, _localization);

            var message = selected is { Count: > 0 }
                ? _localization.Get("HistoryExportSuccessSelected", entries.Count)
                : _localization.Get("HistoryExportSuccessAll", entries.Count);

            _dialogs.Show(AppDialogKind.Info, _localization.Get("HistoryExportTitle"), message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geçmiş Excel dışa aktarımı başarısız.");
            _dialogs.Show(AppDialogKind.Error, _localization.Get("HistoryExportTitle"), _localization.Get("HistoryExportFailed"));
        }
    }
}
