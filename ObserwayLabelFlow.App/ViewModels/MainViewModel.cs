using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ObserwayLabelFlow.App.Services;
using ObserwayLabelFlow.App.Views;
using ObserwayLabelFlow.Core.Configuration;
using ObserwayLabelFlow.Core.History;
using ObserwayLabelFlow.Core.Security;
using ObserwayLabelFlow.Core.Warehouse;

namespace ObserwayLabelFlow.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITokenStore _tokenStore;
    private readonly IHistoryService _history;
    private readonly IInboundHistoryService _inboundHistory;
    private readonly ILocalizationService _localization;
    private readonly IWarehouseApiClient _warehouseApiClient;
    private readonly IWarehouseSession _warehouseSession;
    private readonly IUserSettingsStore _userSettings;
    private readonly IApiBaseUrlProvider _apiBaseUrl;
    private readonly IAppDialogService _dialogs;
    private readonly IToastService _toasts;
    private readonly IHistoryExportService _historyExport;
    private readonly ILogger<MainViewModel> _logger;

    private string? _welcomeDisplayName;
    private string? _lastQueriedTracking;
    private bool _productSummaryIsDefaultHint = true;
    private bool _suppressCultureSelection;
    private UserAppSettings? _currentSettings;
    private PrintHistoryEntry? _lastHistoryEntry;
    private PrintHistoryEntry? _historyContextEntry;
    private string? _historyContextCellValue;
    private long? _pendingWorkspaceOrderId;
    private string? _pendingWorkspaceReference;
    private int? _displayedOutboundOrderStatus;
    private int? _displayedInboundOrderStatus;

    public PrintHistoryEntry? HistoryContextEntry => _historyContextEntry;

    public MainViewModel(
        ITokenStore tokenStore,
        IHistoryService history,
        IInboundHistoryService inboundHistory,
        ILocalizationService localization,
        IWarehouseApiClient warehouseApiClient,
        IWarehouseSession warehouseSession,
        IUserSettingsStore userSettings,
        IApiBaseUrlProvider apiBaseUrl,
        IAppDialogService dialogs,
        IToastService toasts,
        IHistoryExportService historyExport,
        ILogger<MainViewModel> logger)
    {
        _tokenStore = tokenStore;
        _history = history;
        _inboundHistory = inboundHistory;
        _localization = localization;
        _warehouseApiClient = warehouseApiClient;
        _warehouseSession = warehouseSession;
        _warehouseSession.Changed += OnWarehouseSessionChanged;
        _userSettings = userSettings;
        _apiBaseUrl = apiBaseUrl;
        _dialogs = dialogs;
        _toasts = toasts;
        _historyExport = historyExport;
        _logger = logger;
        ProductSummary = _localization.Get("ProductSummaryHint");
        UserDisplayName = _localization.Get("UserGuest");
        RefreshInboundHistoryDayLabel();
        RefreshHistoryDayLabel();
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
        RefreshLocalizedOrderStatusDisplays();
        RefreshInboundHistoryDayLabel();
        RefreshHistoryDayLabel();
    }

    private void RefreshLocalizedOrderStatusDisplays()
    {
        if (_displayedOutboundOrderStatus is int outboundStatus)
            CurrentOrderStatus = OrderStatusLocalizer.GetDisplay(_localization, outboundStatus);

        if (_displayedInboundOrderStatus is int inboundStatus)
            InboundOrderStatusDisplay = OrderStatusLocalizer.GetDisplay(_localization, inboundStatus);

    }

    private void OnWarehouseSessionChanged(object? sender, EventArgs e)
        => RefreshWarehouseUiState();

    public event Action? LogoutRequested;
    public event Action? PrintRequested;

    [ObservableProperty]
    private AppWorkspaceMode currentMode = AppWorkspaceMode.WarehouseSelect;

    public bool IsWarehouseSelectVisible => CurrentMode == AppWorkspaceMode.WarehouseSelect;
    public bool IsModeSelectVisible => CurrentMode == AppWorkspaceMode.ModeSelect;
    public bool ShowProductDetailsOnInbound => _warehouseSession.ShowProductDetailsOnInbound;
    public bool IsSimpleInboundVisible => CurrentMode == AppWorkspaceMode.Inbound && !ShowProductDetailsOnInbound;
    public bool IsDetailedInboundVisible => CurrentMode == AppWorkspaceMode.Inbound && ShowProductDetailsOnInbound;
    public bool IsInboundVisible => IsSimpleInboundVisible;
    public bool IsOutboundVisible => CurrentMode == AppWorkspaceMode.Outbound;
    /// <summary>Detaylı ürün+etiket yalnız girişte (showProductDetailsOnInbound).</summary>
    public bool IsDetailedWorkspaceVisible => IsDetailedInboundVisible;
    public bool IsWorkspaceChromeVisible => CurrentMode is AppWorkspaceMode.Inbound
        or AppWorkspaceMode.Outbound;

    partial void OnCurrentModeChanged(AppWorkspaceMode value)
    {
        OnPropertyChanged(nameof(IsWarehouseSelectVisible));
        OnPropertyChanged(nameof(IsModeSelectVisible));
        OnPropertyChanged(nameof(IsSimpleInboundVisible));
        OnPropertyChanged(nameof(IsDetailedInboundVisible));
        OnPropertyChanged(nameof(IsInboundVisible));
        OnPropertyChanged(nameof(IsOutboundVisible));
        OnPropertyChanged(nameof(IsDetailedWorkspaceVisible));
        OnPropertyChanged(nameof(IsWorkspaceChromeVisible));
        IsUserMenuOpen = false;
    }

    public ObservableCollection<WarehouseDto> AvailableWarehouses { get; } = new();

    [ObservableProperty]
    private string warehouseSelectStatusMessage = string.Empty;

    [ObservableProperty]
    private string selectedWarehouseDisplayName = string.Empty;

    [ObservableProperty]
    private bool canSelectInbound;

    [ObservableProperty]
    private bool canSelectOutbound;

    [ObservableProperty]
    private bool isInboundUnmatched;

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
            ClearOperationCommand.NotifyCanExecuteChanged();
            return;
        }

        var cleaned = value.Trim().Trim('\r', '\n', '\t');
        if (cleaned != value)
            TrackingNumber = cleaned;

        QueryAndPrintCommand.NotifyCanExecuteChanged();
        ClearOperationCommand.NotifyCanExecuteChanged();
    }

    private bool _suppressInboundMatchReset;

    [ObservableProperty]
    private string inboundQuery = string.Empty;

    partial void OnInboundQueryChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var cleaned = value.Trim().Trim('\r', '\n', '\t');
            if (cleaned != value)
                InboundQuery = cleaned;
        }

        if (!_suppressInboundMatchReset)
        {
            IsObsMatched = false;
            IsInboundReturnProduct = false;
            IsInboundUnmatched = false;
            _displayedInboundOrderStatus = null;
            InboundOrderStatusDisplay = string.Empty;
        }

        LookupInboundCommand.NotifyCanExecuteChanged();
        ClearInboundCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private bool isObsMatched;

    [ObservableProperty]
    private bool isInboundReturnProduct;

    [ObservableProperty]
    private string inboundStatusMessage = string.Empty;

    [ObservableProperty]
    private string inboundOrderStatusDisplay = string.Empty;

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
    private string currentOrderStatus = string.Empty;

    [ObservableProperty]
    private string displayedTrackingNumber = string.Empty;

    [ObservableProperty]
    private bool labelReady;

    partial void OnLabelReadyChanged(bool value)
        => ClearOperationCommand.NotifyCanExecuteChanged();

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
        ClearOperationCommand.NotifyCanExecuteChanged();
        LabelPreviewChanged?.Invoke(value);
    }

    public event Action<Uri?>? LabelPreviewChanged;

    [ObservableProperty]
    private bool isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        QueryAndPrintCommand.NotifyCanExecuteChanged();
        PrintLabelCommand.NotifyCanExecuteChanged();
        ClearOperationCommand.NotifyCanExecuteChanged();
        LookupInboundCommand.NotifyCanExecuteChanged();
        ClearInboundCommand.NotifyCanExecuteChanged();
        NotifyTransferCommands();
    }

    [ObservableProperty]
    private bool hasActiveOrder;

    partial void OnHasActiveOrderChanged(bool value)
        => ClearOperationCommand.NotifyCanExecuteChanged();

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
    private DateTime historyDay = DateTime.Today;

    [ObservableProperty]
    private string historyDayLabel = string.Empty;

    [ObservableProperty]
    private int historyRecordCount;

    [ObservableProperty]
    private bool historyOnlyErrors;

    [ObservableProperty]
    private bool? allHistorySelected;

    [ObservableProperty]
    private int selectedHistoryCount;

    [ObservableProperty]
    private int productItemCount;

    public ObservableCollection<PrintHistoryEntry> History { get; } = new();

    public bool CanGoHistoryNextDay => HistoryDay.Date < DateTime.Today;

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
        await EnsureWarehouseSessionAsync(ct);
        await RefreshHistoryAsync(ct);
        await RefreshInboundHistoryAsync(ct);
    }

    public async Task ReloadSettingsFromStoreAsync(CancellationToken ct = default)
    {
        await LoadSettingsAsync(ct);
    }

    private async Task EnsureWarehouseSessionAsync(CancellationToken ct = default)
    {
        await _warehouseSession.ReloadAvailableAsync(ct);
        AvailableWarehouses.Clear();
        foreach (var warehouse in _warehouseSession.Available)
            AvailableWarehouses.Add(warehouse);

        RefreshWarehouseUiState();
        CurrentMode = _warehouseSession.HasSelection
            ? AppWorkspaceMode.ModeSelect
            : AppWorkspaceMode.WarehouseSelect;

        WarehouseSelectStatusMessage = AvailableWarehouses.Count == 0
            ? _localization.Get("Warehouse_NoneAssigned")
            : string.Empty;
    }

    private void RefreshWarehouseUiState()
    {
        SelectedWarehouseDisplayName = _warehouseSession.SelectedDisplayName;
        CanSelectInbound = _warehouseSession.HasSelection && _warehouseSession.AllowInbound;
        CanSelectOutbound = _warehouseSession.HasSelection && _warehouseSession.AllowOutbound;
        OnPropertyChanged(nameof(ShowProductDetailsOnInbound));
        OnPropertyChanged(nameof(IsSimpleInboundVisible));
        OnPropertyChanged(nameof(IsDetailedInboundVisible));
        OnPropertyChanged(nameof(IsInboundVisible));
        OnPropertyChanged(nameof(IsOutboundVisible));
        OnPropertyChanged(nameof(IsDetailedWorkspaceVisible));
        OnPropertyChanged(nameof(IsWorkspaceChromeVisible));
    }

    [RelayCommand]
    private async Task ReloadWarehousesAsync()
        => await EnsureWarehouseSessionAsync();

    [RelayCommand]
    private async Task SelectWarehouseAsync(WarehouseDto? warehouse)
    {
        if (warehouse is null)
            return;

        await _warehouseSession.SelectAsync(warehouse);
        RefreshWarehouseUiState();
        CurrentMode = AppWorkspaceMode.ModeSelect;
    }

    [RelayCommand]
    private async Task ChangeWarehouseAsync()
    {
        IsUserMenuOpen = false;
        await _warehouseSession.ClearSelectionAsync();
        CurrentMode = AppWorkspaceMode.WarehouseSelect;
        await EnsureWarehouseSessionAsync();
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
        // Detaylı etiket/ürün workspace yalnızca ürün girişinde kullanılır.
        if (!IsDetailedInboundVisible)
            return false;

        if (string.IsNullOrWhiteSpace(TrackingNumber))
        {
            IsInboundReturnProduct = false;
            ProductSummary = _localization.Get("ProductSummaryEmptyTracking");
            _productSummaryIsDefaultHint = false;
            ClearCurrentOrderInfo();
            ProductItems.Clear();
            _dialogs.Show(AppDialogKind.Warning, _localization.Get("Query_NotFoundTitle"), ProductSummary);
            return false;
        }

        IsBusy = true;
        IsInboundReturnProduct = false;
        SelectedTabIndex = 0;
        _lastHistoryEntry = null;
        _pendingWorkspaceOrderId = null;
        _pendingWorkspaceReference = null;
        try
        {
            var tn = TrackingNumber.Trim();
            _lastQueriedTracking = tn;
            DisplayedTrackingNumber = tn;
            _productSummaryIsDefaultHint = false;

            var warehouseId = _warehouseSession.SelectedWarehouseId;
            if (warehouseId <= 0)
            {
                var notSelected = _localization.Get("Warehouse_NotSelected");
                ProductItems.Clear();
                ProductSummary = notSelected;
                ClearCurrentOrderInfo();
                LastQueryFailedMessage = notSelected;
                LastQuerySucceeded = false;
                _dialogs.Show(
                    AppDialogKind.Warning,
                    _localization.Get("Query_NotFoundTitle"),
                    notSelected);
                return false;
            }

            var result = await _warehouseApiClient.LookupAsync(tn, warehouseId, CancellationToken.None);
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
            var order = result.Value;
            IsInboundReturnProduct = order.Matched && order.IsCancelledOrReturned;

            if (!order.Matched)
            {
                HasActiveOrder = true;
                ProductSummary = _localization.Get("ProductSummaryAfterQuery", tn);
                ClearCurrentOrderInfo();
                HasActiveOrder = true;
                DisplayedTrackingNumber = tn;
                ProductItems.Clear();
                ProductItems.Add(new ProductPreviewItem
                {
                    OfficialName = _localization.Get("ProductSummaryEmptyProducts")
                });
                ProductItemCount = 0;
                _pendingWorkspaceOrderId = null;
                _pendingWorkspaceReference = null;

                _dialogs.Show(
                    AppDialogKind.Warning,
                    _localization.Get("ModeSelect_ProductInboundTitle"),
                    _localization.Get("Inbound_UnmatchedWarning"));
                return await PostDetailedInboundAsync(order, tn, warehouseId);
            }

            HasActiveOrder = true;
            ApplyWarehouseLookupToUi(order, tn);

            if (!await PostDetailedInboundAsync(order, tn, warehouseId))
                return false;

            if (PdfSource is not null)
            {
                var labelSettings = _currentSettings?.LabelPrintSettings ?? new LabelPrintSettings();
                _lastHistoryEntry = OrderPresentationMapper.CreateHistoryEntry(
                    order,
                    tn,
                    labelSettings,
                    _welcomeDisplayName,
                    _localization.Get("HistoryNotesSample"),
                    _localization);

                await _history.AddAsync(_lastHistoryEntry, CancellationToken.None);
                if (HistoryDay.Date != DateTime.Today)
                    HistoryDay = DateTime.Today;
                else
                    await RefreshHistoryAsync(CancellationToken.None);
            }

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

    private async Task<bool> PostDetailedInboundAsync(WarehouseLookupDto order, string reference, long warehouseId)
    {
        var post = await _warehouseApiClient.PostInboundAsync(
            new WarehouseInboundRequest(
                warehouseId,
                reference,
                order.Matched ? order.OrderId : null,
                InboundTrackingNumber: reference),
            CancellationToken.None);
        if (!post.IsSuccess || post.Value is null)
        {
            var error = post.Errors.FirstOrDefault() ?? _localization.Get("Error_Connection");
            ProductSummary = error;
            LastQueryFailedMessage = error;
            LastQuerySucceeded = false;
            await SaveInboundHistoryAsync(reference, order.OrderNumber, false, error);
            _dialogs.Show(
                AppDialogKind.Warning,
                _localization.Get("ModeSelect_ProductInboundTitle"),
                error);
            return false;
        }

        if (post.Value.AlreadyExists)
        {
            var already = _localization.Get(
                "Inbound_AlreadyExists",
                post.Value.OrderNumber ?? order.OrderNumber ?? reference);
            ProductSummary = already;
            LastQuerySucceeded = true;
            LastQueryFailedMessage = null;
            _dialogs.Show(
                AppDialogKind.Warning,
                _localization.Get("ModeSelect_ProductInboundTitle"),
                already);
            return true;
        }

        var orderNumber = post.Value.OrderNumber ?? order.OrderNumber;
        var message = order.Matched
            ? order.IsCancelledOrReturned
                ? _localization.Get("Inbound_ReturnProductMarked", orderNumber)
                : _localization.Get("Inbound_MarkedSuccess", orderNumber)
            : _localization.Get("Inbound_UnmatchedSaved", reference);
        await SaveInboundHistoryAsync(reference, post.Value.OrderNumber ?? order.OrderNumber, true, null);
        _dialogs.Show(
            order.IsCancelledOrReturned ? AppDialogKind.Warning : AppDialogKind.Info,
            _localization.Get("ModeSelect_ProductInboundTitle"),
            message);
        return true;
    }

    [ObservableProperty]
    private bool lastQuerySucceeded;

    [ObservableProperty]
    private string? lastQueryFailedMessage;

    private async Task RefreshHistoryAsync(CancellationToken ct = default)
    {
        try
        {
            var rows = await _history.GetForDayAsync(new HistoryFilter
            {
                DayLocal = DateOnly.FromDateTime(HistoryDay.Date),
                SearchText = HistorySearchText,
                OnlyErrors = HistoryOnlyErrors,
                Take = 2000
            }, ct);
            ReplaceHistoryRows(rows);
            HistoryRecordCount = History.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geçmiş yüklenirken hata oluştu.");
            ReplaceHistoryRows([]);
            HistoryRecordCount = 0;
        }
    }

    partial void OnHistoryDayChanged(DateTime value)
    {
        var normalized = value.Date;
        if (normalized != value)
        {
            HistoryDay = normalized;
            return;
        }

        RefreshHistoryDayLabel();
        OnPropertyChanged(nameof(CanGoHistoryNextDay));
        HistoryPreviousDayCommand.NotifyCanExecuteChanged();
        HistoryNextDayCommand.NotifyCanExecuteChanged();
        _ = RefreshHistoryAsync();
    }

    private void RefreshHistoryDayLabel()
    {
        HistoryDayLabel = HistoryDay.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private void HistoryPreviousDay()
    {
        HistoryDay = HistoryDay.Date.AddDays(-1);
    }

    [RelayCommand(CanExecute = nameof(CanHistoryNextDay))]
    private void HistoryNextDay()
    {
        var next = HistoryDay.Date.AddDays(1);
        if (next > DateTime.Today)
            return;
        HistoryDay = next;
    }

    private bool CanHistoryNextDay() => HistoryDay.Date < DateTime.Today;

    [RelayCommand]
    private void HistoryGoToday()
    {
        HistoryDay = DateTime.Today;
    }

    [RelayCommand]
    private async Task ApplyHistoryFilterAsync()
        => await RefreshHistoryAsync();

    partial void OnHistoryOnlyErrorsChanged(bool value)
    {
        if (!_clearingHistoryFilter)
            _ = RefreshHistoryAsync();
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
        HistoryRecordCount = History.Count;
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
    private async Task ClearHistoryFilterAsync()
    {
        _clearingHistoryFilter = true;
        try
        {
            HistorySearchText = string.Empty;
            HistoryOnlyErrors = false;
            AllHistorySelected = false;
        }
        finally
        {
            _clearingHistoryFilter = false;
        }

        await RefreshHistoryAsync();
    }

    public async Task UpdateLastPrintResultAsync(bool success, string? errorMessage = null)
    {
        if (_lastHistoryEntry is not null)
        {
            try
            {
                _lastHistoryEntry.Success = success;
                _lastHistoryEntry.ErrorMessage = errorMessage;
                await _history.UpdateAsync(_lastHistoryEntry, CancellationToken.None);
                if (HistoryDay.Date != DateTime.Today)
                    HistoryDay = DateTime.Today;
                else
                    await RefreshHistoryAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Yazdırma sonucu güncellenirken hata oluştu.");
            }
        }

        if (!success)
            return;

        // Detaylı girişte kayıt genelde sorguda atılır; pending varsa yazdırma sonrası tamamlanır.
        if (IsDetailedInboundVisible)
            await MarkInboundReadyAfterPrintAsync();
    }

    private async Task MarkInboundReadyAfterPrintAsync()
    {
        var reference = _pendingWorkspaceReference;
        if (string.IsNullOrWhiteSpace(reference))
            return;

        var warehouseId = _warehouseSession.SelectedWarehouseId;
        if (warehouseId <= 0)
        {
            _dialogs.Show(
                AppDialogKind.Warning,
                _localization.Get("ModeSelect_ProductInboundTitle"),
                _localization.Get("Warehouse_NotSelected"));
            return;
        }

        var result = await _warehouseApiClient.PostInboundAsync(
            new WarehouseInboundRequest(
                warehouseId,
                reference,
                _pendingWorkspaceOrderId,
                InboundTrackingNumber: reference),
            CancellationToken.None);
        if (!result.IsSuccess || result.Value is null)
        {
            _dialogs.Show(
                AppDialogKind.Warning,
                _localization.Get("ModeSelect_ProductInboundTitle"),
                result.Errors.FirstOrDefault() ?? _localization.Get("Error_Connection"));
            return;
        }

        _pendingWorkspaceOrderId = null;
        _pendingWorkspaceReference = null;
        if (result.Value.AlreadyExists)
        {
            _dialogs.Show(
                AppDialogKind.Warning,
                _localization.Get("ModeSelect_ProductInboundTitle"),
                _localization.Get(
                    "Inbound_AlreadyExists",
                    result.Value.OrderNumber ?? reference));
            return;
        }

        _dialogs.Show(
            AppDialogKind.Info,
            _localization.Get("ModeSelect_ProductInboundTitle"),
            _localization.Get("Inbound_MarkedSuccess", result.Value.OrderNumber));
    }

    [RelayCommand(CanExecute = nameof(CanQueryAndPrint))]
    public async Task<bool> QueryAndPrintAsync()
    {
        var ok = await QueryAsync();
        if (ok && PdfSource is not null && AutoPrintOnQuery)
            RequestPrint();
        return ok;
    }

    private bool CanQueryAndPrint()
        => !IsBusy
           && !string.IsNullOrWhiteSpace(TrackingNumber);

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

    [RelayCommand(CanExecute = nameof(CanClearOperation))]
    private void ClearOperation()
    {
        TrackingNumber = string.Empty;
        _lastHistoryEntry = null;
        _lastQueriedTracking = null;
        _productSummaryIsDefaultHint = true;
        ProductSummary = _localization.Get("ProductSummaryHint");
        ProductItems.Clear();
        ClearCurrentOrderInfo();
        IsInboundReturnProduct = false;
        LastQuerySucceeded = false;
        LastQueryFailedMessage = null;
    }

    private bool CanClearOperation()
        => !IsBusy && (HasActiveOrder || LabelReady || !string.IsNullOrWhiteSpace(TrackingNumber));

    private void ApplyWarehouseLookupToUi(WarehouseLookupDto order, string scannedTrackingNumber)
    {
        DisplayedTrackingNumber = scannedTrackingNumber;
        ProductSummary = _localization.Get("ProductSummaryAfterQuery", scannedTrackingNumber);

        CurrentOrderNumber = order.OrderNumber ?? string.Empty;
        CurrentAmazonOrderId = string.Empty;
        CurrentCustomerName = string.Empty;
        CurrentCarrierName = OrderPresentationMapper.FormatCarrierDisplay(order.CarrierName, order.CarrierCode);
        _displayedOutboundOrderStatus = order.OrderStatus;
        CurrentOrderStatus = OrderStatusLocalizer.GetDisplay(_localization, order);

        PdfSource = OrderPresentationMapper.TryCreateLabelUri(order.LabelUrl, _apiBaseUrl.GetBaseUrl());

        ProductItems.Clear();
        var products = order.Products ?? [];
        var apiBaseUrl = _apiBaseUrl.GetBaseUrl();
        foreach (var product in products)
            ProductItems.Add(OrderPresentationMapper.ToProductPreviewItem(product, apiBaseUrl));

        ProductItemCount = products.Count;
        if (products.Count == 0)
        {
            _logger.LogWarning(
                "UI ürün listesi boş. Order={Order} Tracking={Tracking}",
                order.OrderNumber,
                scannedTrackingNumber);
            ProductItems.Add(new ProductPreviewItem { OfficialName = _localization.Get("ProductSummaryEmptyProducts") });
        }
    }

    private void ClearCurrentOrderInfo()
    {
        PdfSource = null;
        CurrentOrderNumber = string.Empty;
        CurrentAmazonOrderId = string.Empty;
        CurrentCustomerName = string.Empty;
        CurrentCarrierName = string.Empty;
        _displayedOutboundOrderStatus = null;
        CurrentOrderStatus = string.Empty;
        DisplayedTrackingNumber = string.Empty;
        LabelReady = false;
        HasActiveOrder = false;
        ProductItemCount = 0;
        ProductItems.Clear();
        _pendingWorkspaceOrderId = null;
        _pendingWorkspaceReference = null;
    }

    [RelayCommand]
    private void SelectProductInbound()
    {
        if (!_warehouseSession.AllowInbound)
            return;

        IsUserMenuOpen = false;
        ClearInboundState();
        if (ShowProductDetailsOnInbound)
        {
            ClearOperation();
            SelectedTabIndex = 0;
        }
        InboundSelectedTabIndex = 0;
        InboundHistoryDay = DateTime.Today;
        CurrentMode = AppWorkspaceMode.Inbound;
        _ = RefreshInboundHistoryAsync();
    }

    [RelayCommand]
    private void SelectProductOutbound()
    {
        if (!_warehouseSession.AllowOutbound)
            return;

        IsUserMenuOpen = false;
        ClearTransferState();
        CurrentMode = AppWorkspaceMode.Outbound;
    }

    [RelayCommand]
    private void ChangeMode()
    {
        IsUserMenuOpen = false;
        CurrentMode = AppWorkspaceMode.ModeSelect;
    }

    [ObservableProperty]
    private int inboundSelectedTabIndex;

    public ObservableCollection<InboundHistoryEntry> InboundHistory { get; } = new();

    [ObservableProperty]
    private bool isInboundHistoryEmpty = true;

    [ObservableProperty]
    private DateTime inboundHistoryDay = DateTime.Today;

    [ObservableProperty]
    private string inboundHistoryDayLabel = string.Empty;

    [ObservableProperty]
    private string inboundHistorySearchText = string.Empty;

    [ObservableProperty]
    private int inboundHistoryRecordCount;

    public bool CanGoInboundHistoryNextDay => InboundHistoryDay.Date < DateTime.Today;

    partial void OnInboundHistoryDayChanged(DateTime value)
    {
        var normalized = value.Date;
        if (normalized != value)
        {
            InboundHistoryDay = normalized;
            return;
        }

        RefreshInboundHistoryDayLabel();
        OnPropertyChanged(nameof(CanGoInboundHistoryNextDay));
        InboundHistoryPreviousDayCommand.NotifyCanExecuteChanged();
        InboundHistoryNextDayCommand.NotifyCanExecuteChanged();
        _ = RefreshInboundHistoryAsync();
    }

    private void RefreshInboundHistoryDayLabel()
    {
        InboundHistoryDayLabel = InboundHistoryDay.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private void InboundHistoryPreviousDay()
    {
        InboundHistoryDay = InboundHistoryDay.Date.AddDays(-1);
    }

    [RelayCommand(CanExecute = nameof(CanInboundHistoryNextDay))]
    private void InboundHistoryNextDay()
    {
        var next = InboundHistoryDay.Date.AddDays(1);
        if (next > DateTime.Today)
            return;
        InboundHistoryDay = next;
    }

    private bool CanInboundHistoryNextDay() => InboundHistoryDay.Date < DateTime.Today;

    [RelayCommand]
    private void InboundHistoryGoToday()
    {
        InboundHistoryDay = DateTime.Today;
    }

    [RelayCommand]
    private async Task ApplyInboundHistorySearchAsync()
        => await RefreshInboundHistoryAsync();

    [RelayCommand]
    private async Task ClearInboundHistorySearchAsync()
    {
        InboundHistorySearchText = string.Empty;
        await RefreshInboundHistoryAsync();
    }

    private async Task RefreshInboundHistoryAsync(CancellationToken ct = default)
    {
        try
        {
            var rows = await _inboundHistory.GetForDayAsync(new InboundHistoryFilter
            {
                DayLocal = DateOnly.FromDateTime(InboundHistoryDay.Date),
                SearchText = InboundHistorySearchText,
                Take = 2000
            }, ct);

            InboundHistory.Clear();
            foreach (var row in rows)
                InboundHistory.Add(row);

            InboundHistoryRecordCount = InboundHistory.Count;
            IsInboundHistoryEmpty = InboundHistory.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ürün girişi geçmişi yüklenemedi.");
            InboundHistory.Clear();
            InboundHistoryRecordCount = 0;
            IsInboundHistoryEmpty = true;
        }
    }

    private async Task SaveInboundHistoryAsync(string reference, string? orderNumber, bool success, string? errorMessage)
    {
        try
        {
            await _inboundHistory.AddAsync(new InboundHistoryEntry
            {
                Reference = reference,
                OrderNumber = orderNumber,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Success = success,
                ErrorMessage = errorMessage,
                MarkedBy = _welcomeDisplayName
            });

            if (InboundHistoryDay.Date != DateTime.Today)
                InboundHistoryDay = DateTime.Today;
            else
                await RefreshInboundHistoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ürün girişi geçmiş kaydı yazılamadı.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanLookupInbound))]
    private async Task LookupInboundAsync()
    {
        var query = InboundQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return;

        IsBusy = true;
        IsObsMatched = false;
        IsInboundReturnProduct = false;
        IsInboundUnmatched = false;
        InboundStatusMessage = string.Empty;
        _displayedInboundOrderStatus = null;
        InboundOrderStatusDisplay = string.Empty;
        try
        {
            var warehouseId = _warehouseSession.SelectedWarehouseId;
            if (warehouseId <= 0)
            {
                InboundStatusMessage = _localization.Get("Warehouse_NotSelected");
                await SaveInboundHistoryAsync(query, null, false, InboundStatusMessage);
                _dialogs.Show(
                    AppDialogKind.Warning,
                    _localization.Get("ModeSelect_ProductInboundTitle"),
                    InboundStatusMessage);
                return;
            }

            var lookup = await _warehouseApiClient.LookupAsync(query, warehouseId);
            if (!lookup.IsSuccess || lookup.Value is null)
            {
                InboundStatusMessage = lookup.Errors.FirstOrDefault() ?? _localization.Get("Error_Connection");
                await SaveInboundHistoryAsync(query, null, false, InboundStatusMessage);
                _dialogs.Show(
                    AppDialogKind.Warning,
                    _localization.Get("ModeSelect_ProductInboundTitle"),
                    InboundStatusMessage);
                return;
            }

            var order = lookup.Value;
            IsInboundUnmatched = !order.Matched;
            if (order.Matched)
            {
                _displayedInboundOrderStatus = order.OrderStatus;
                InboundOrderStatusDisplay = OrderStatusLocalizer.GetDisplay(_localization, order);
                IsInboundReturnProduct = order.IsCancelledOrReturned;
            }
            else
            {
                _displayedInboundOrderStatus = null;
                InboundOrderStatusDisplay = string.Empty;
                IsInboundReturnProduct = false;
            }

            var post = await _warehouseApiClient.PostInboundAsync(
                new WarehouseInboundRequest(
                    warehouseId,
                    query,
                    order.Matched ? order.OrderId : null,
                    InboundTrackingNumber: query));
            if (!post.IsSuccess || post.Value is null)
            {
                InboundStatusMessage = post.Errors.FirstOrDefault() ?? _localization.Get("Error_Connection");
                await SaveInboundHistoryAsync(query, order.OrderNumber, false, InboundStatusMessage);
                _dialogs.Show(
                    AppDialogKind.Warning,
                    _localization.Get("ModeSelect_ProductInboundTitle"),
                    InboundStatusMessage);
                return;
            }

            IsObsMatched = true;
            if (post.Value.AlreadyExists)
            {
                InboundStatusMessage = _localization.Get(
                    "Inbound_AlreadyExists",
                    post.Value.OrderNumber ?? order.OrderNumber ?? query);
                _dialogs.Show(
                    AppDialogKind.Warning,
                    _localization.Get("ModeSelect_ProductInboundTitle"),
                    InboundStatusMessage);
            }
            else if (order.Matched)
            {
                var orderNumber = post.Value.OrderNumber ?? order.OrderNumber;
                InboundStatusMessage = IsInboundReturnProduct
                    ? _localization.Get("Inbound_ReturnProductMarked", orderNumber)
                    : _localization.Get("Inbound_MarkedSuccess", orderNumber);
                await SaveInboundHistoryAsync(query, post.Value.OrderNumber ?? order.OrderNumber, true, null);
                _dialogs.Show(
                    IsInboundReturnProduct ? AppDialogKind.Warning : AppDialogKind.Info,
                    _localization.Get("ModeSelect_ProductInboundTitle"),
                    InboundStatusMessage);
            }
            else
            {
                InboundStatusMessage = _localization.Get("Inbound_UnmatchedSaved", query);
                await SaveInboundHistoryAsync(query, post.Value.OrderNumber ?? order.OrderNumber, true, null);
                _dialogs.Show(
                    AppDialogKind.Info,
                    _localization.Get("ModeSelect_ProductInboundTitle"),
                    InboundStatusMessage);
            }

            _suppressInboundMatchReset = true;
            try
            {
                InboundQuery = string.Empty;
            }
            finally
            {
                _suppressInboundMatchReset = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ürün girişi işaretleme başarısız.");
            InboundStatusMessage = _localization.Get("Error_Connection");
            await SaveInboundHistoryAsync(query, null, false, InboundStatusMessage);
            _dialogs.Show(
                AppDialogKind.Error,
                _localization.Get("ModeSelect_ProductInboundTitle"),
                InboundStatusMessage);
        }
        finally
        {
            IsBusy = false;
            ClearInboundCommand.NotifyCanExecuteChanged();
            LookupInboundCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanLookupInbound()
        => !IsBusy && !string.IsNullOrWhiteSpace(InboundQuery);

    [RelayCommand(CanExecute = nameof(CanClearInbound))]
    private void ClearInbound() => ClearInboundState();

    private bool CanClearInbound()
        => !IsBusy
           && (!string.IsNullOrWhiteSpace(InboundQuery)
               || IsObsMatched
               || IsInboundReturnProduct
               || IsInboundUnmatched
               || !string.IsNullOrWhiteSpace(InboundStatusMessage));

    private void ClearInboundState()
    {
        InboundQuery = string.Empty;
        IsObsMatched = false;
        IsInboundReturnProduct = false;
        IsInboundUnmatched = false;
        InboundStatusMessage = string.Empty;
        _displayedInboundOrderStatus = null;
        InboundOrderStatusDisplay = string.Empty;
        ClearInboundCommand.NotifyCanExecuteChanged();
        LookupInboundCommand.NotifyCanExecuteChanged();
    }

    public void ClearInboundTrackingForNextScan()
    {
        _suppressInboundMatchReset = true;
        try
        {
            InboundQuery = string.Empty;
        }
        finally
        {
            _suppressInboundMatchReset = false;
        }

        ClearInboundCommand.NotifyCanExecuteChanged();
        LookupInboundCommand.NotifyCanExecuteChanged();
    }

    private bool _suppressTransferMatchReset;

    [ObservableProperty]
    private string transferQuery = string.Empty;

    partial void OnTransferQueryChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var cleaned = value.Trim().Trim('\r', '\n', '\t');
            if (cleaned != value)
                TransferQuery = cleaned;
        }

        if (!_suppressTransferMatchReset)
        {
            IsTransferSuccess = false;
            IsTransferBlocked = false;
            CanTransferLoad = false;
            TransferLookupOrderId = null;
            _transferLookupMatched = false;
        }

        NotifyTransferCommands();
    }

    [ObservableProperty]
    private string transferVehicleName = string.Empty;

    partial void OnTransferVehicleNameChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var cleaned = value.Trim();
            if (cleaned.Length > 100)
                cleaned = cleaned[..100];
            if (cleaned != value)
                TransferVehicleName = cleaned;
        }

        NotifyTransferCommands();
    }

    [ObservableProperty]
    private string transferStatusMessage = string.Empty;

    [ObservableProperty]
    private string transferOrderStatusDisplay = string.Empty;

    [ObservableProperty]
    private string transferBlockReason = string.Empty;

    [ObservableProperty]
    private bool isTransferBlocked;

    [ObservableProperty]
    private bool isTransferSuccess;

    [ObservableProperty]
    private bool canTransferLoad;

    private long? TransferLookupOrderId { get; set; }
    private bool _transferLookupMatched;

    [RelayCommand(CanExecute = nameof(CanLookupTransfer))]
    private async Task LookupTransferAsync()
    {
        var query = TransferQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return;

        IsBusy = true;
        IsTransferSuccess = false;
        IsTransferBlocked = false;
        CanTransferLoad = false;
        TransferStatusMessage = string.Empty;
        TransferOrderStatusDisplay = string.Empty;
        TransferBlockReason = string.Empty;
        TransferLookupOrderId = null;
        _transferLookupMatched = false;
        try
        {
            var warehouseId = _warehouseSession.SelectedWarehouseId;
            if (warehouseId <= 0)
            {
                TransferStatusMessage = _localization.Get("Warehouse_NotSelected");
                _dialogs.Show(AppDialogKind.Warning, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
                return;
            }

            var lookup = await _warehouseApiClient.LookupAsync(query, warehouseId);
            if (!lookup.IsSuccess || lookup.Value is null)
            {
                TransferStatusMessage = lookup.Errors.FirstOrDefault() ?? _localization.Get("Error_Connection");
                _dialogs.Show(AppDialogKind.Warning, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
                return;
            }

            var order = lookup.Value;
            _transferLookupMatched = order.Matched;
            TransferLookupOrderId = order.Matched ? order.OrderId : null;
            TransferOrderStatusDisplay = order.Matched
                ? OrderStatusLocalizer.GetDisplay(_localization, order)
                : string.Empty;

            if (order.Matched && order.IsCancelledOrReturned)
            {
                IsTransferBlocked = true;
                TransferBlockReason = string.IsNullOrWhiteSpace(order.BlockReason)
                    ? _localization.Get("Transfer_ReturnOrCancelled")
                    : order.BlockReason!;
                TransferStatusMessage = TransferBlockReason;
                _dialogs.Show(AppDialogKind.Warning, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
                return;
            }

            if (!order.Matched)
            {
                TransferStatusMessage = _localization.Get("Outbound_UnmatchedWarning");
                _dialogs.Show(AppDialogKind.Warning, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
            }
            else
            {
                TransferStatusMessage = _localization.Get("Transfer_ReadyToLoad", order.OrderNumber ?? query);
            }

            CanTransferLoad = true;

            if (!string.IsNullOrWhiteSpace(TransferVehicleName))
                await ConfirmOutboundCoreAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ürün çıkış lookup başarısız.");
            TransferStatusMessage = _localization.Get("Error_Connection");
            _dialogs.Show(AppDialogKind.Error, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
        }
        finally
        {
            IsBusy = false;
            NotifyTransferCommands();
        }
    }

    private bool CanLookupTransfer()
        => !IsBusy && !string.IsNullOrWhiteSpace(TransferQuery);

    /// <summary>Ürün çıkış onayı — araç adı + takip ile POST /outbound.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirmOutbound))]
    private async Task LoadToVehicleAsync()
    {
        if (string.IsNullOrWhiteSpace(TransferQuery) && TransferLookupOrderId is null)
            return;

        if (!CanTransferLoad && !string.IsNullOrWhiteSpace(TransferQuery))
        {
            await LookupTransferAsync();
            return;
        }

        IsBusy = true;
        try
        {
            await ConfirmOutboundCoreAsync(TransferQuery?.Trim() ?? string.Empty);
        }
        finally
        {
            IsBusy = false;
            NotifyTransferCommands();
        }
    }

    private bool CanConfirmOutbound()
        => !IsBusy
           && !IsTransferBlocked
           && !string.IsNullOrWhiteSpace(TransferVehicleName)
           && (CanTransferLoad || !string.IsNullOrWhiteSpace(TransferQuery));

    private async Task ConfirmOutboundCoreAsync(string reference)
    {
        var vehicleName = TransferVehicleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vehicleName))
        {
            TransferStatusMessage = _localization.Get("Transfer_VehicleRequired");
            _dialogs.Show(AppDialogKind.Warning, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
            return;
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            TransferStatusMessage = _localization.Get("ProductSummaryEmptyTracking");
            _dialogs.Show(AppDialogKind.Warning, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
            return;
        }

        var warehouseId = _warehouseSession.SelectedWarehouseId;
        if (warehouseId <= 0)
        {
            TransferStatusMessage = _localization.Get("Warehouse_NotSelected");
            _dialogs.Show(AppDialogKind.Warning, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
            return;
        }

        var result = await _warehouseApiClient.PostOutboundAsync(
            new WarehouseOutboundRequest(
                warehouseId,
                reference,
                TransferLookupOrderId,
                VehicleName: vehicleName));
        if (!result.IsSuccess || result.Value is null)
        {
            TransferStatusMessage = result.Errors.FirstOrDefault() ?? _localization.Get("Error_Connection");
            IsTransferSuccess = false;
            _dialogs.Show(AppDialogKind.Warning, _localization.Get("ModeSelect_ProductOutboundTitle"), TransferStatusMessage);
            return;
        }

        var savedVehicle = string.IsNullOrWhiteSpace(result.Value.VehicleName)
            ? vehicleName
            : result.Value.VehicleName.Trim();
        IsTransferSuccess = true;
        CanTransferLoad = false;
        if (result.Value.AlreadyExists)
        {
            TransferStatusMessage = _localization.Get(
                "Outbound_AlreadyExists",
                result.Value.OrderNumber ?? reference);
            _dialogs.Show(
                AppDialogKind.Warning,
                _localization.Get("ModeSelect_ProductOutboundTitle"),
                TransferStatusMessage);
        }
        else
        {
            TransferStatusMessage = result.Value.Matched
                ? _localization.Get("Outbound_MarkedWithVehicle", result.Value.OrderNumber ?? reference, savedVehicle)
                : _localization.Get("Outbound_UnmatchedSavedWithVehicle", reference, savedVehicle);
            _dialogs.Show(
                AppDialogKind.Info,
                _localization.Get("ModeSelect_ProductOutboundTitle"),
                TransferStatusMessage);
        }

        _suppressTransferMatchReset = true;
        try
        {
            TransferQuery = string.Empty;
        }
        finally
        {
            _suppressTransferMatchReset = false;
        }

        TransferLookupOrderId = null;
        _transferLookupMatched = false;
    }

    [RelayCommand(CanExecute = nameof(CanClearTransfer))]
    private void ClearTransfer() => ClearTransferState();

    private bool CanClearTransfer()
        => !IsBusy
           && (!string.IsNullOrWhiteSpace(TransferQuery)
               || !string.IsNullOrWhiteSpace(TransferVehicleName)
               || !string.IsNullOrWhiteSpace(TransferStatusMessage)
               || IsTransferSuccess
               || IsTransferBlocked);

    private void ClearTransferState()
    {
        TransferQuery = string.Empty;
        TransferStatusMessage = string.Empty;
        TransferOrderStatusDisplay = string.Empty;
        TransferBlockReason = string.Empty;
        IsTransferBlocked = false;
        IsTransferSuccess = false;
        CanTransferLoad = false;
        TransferLookupOrderId = null;
        _transferLookupMatched = false;
        NotifyTransferCommands();
    }

    public void ClearTransferTrackingForNextScan()
    {
        _suppressTransferMatchReset = true;
        try
        {
            TransferQuery = string.Empty;
        }
        finally
        {
            _suppressTransferMatchReset = false;
        }

        NotifyTransferCommands();
    }

    private void NotifyTransferCommands()
    {
        LookupTransferCommand.NotifyCanExecuteChanged();
        LoadToVehicleCommand.NotifyCanExecuteChanged();
        ClearTransferCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        IsUserMenuOpen = false;
        try
        {
            await _warehouseSession.ClearSelectionAsync();
            await _tokenStore.ClearAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Çıkış yapılırken token temizlenemedi.");
        }
        LogoutRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ReprintAsync(PrintHistoryEntry? entry)
    {
        if (entry?.PdfUrl is null)
            return;

        _lastHistoryEntry = entry;
        _pendingWorkspaceOrderId = null;
        _pendingWorkspaceReference = null;

        var reference = string.IsNullOrWhiteSpace(entry.TrackingNumber)
            ? entry.OrderNumber
            : entry.TrackingNumber;
        var warehouseId = _warehouseSession.SelectedWarehouseId;
        if (warehouseId > 0 && !string.IsNullOrWhiteSpace(reference))
        {
            try
            {
                var lookup = await _warehouseApiClient.LookupAsync(reference.Trim(), warehouseId);
                if (lookup.IsSuccess && lookup.Value is not null)
                {
                    var order = lookup.Value;
                    if (order.Matched && !string.IsNullOrWhiteSpace(order.LabelUrl))
                    {
                        _pendingWorkspaceOrderId = order.OrderId;
                        _pendingWorkspaceReference = reference.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Yeniden yazdırma öncesi lookup başarısız.");
            }
        }

        PdfSource = OrderPresentationMapper.TryCreateLabelUri(entry.PdfUrl, _apiBaseUrl.GetBaseUrl());
        PrintRequested?.Invoke();
    }

    [RelayCommand]
    private void ShowHistoryError(PrintHistoryEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.ErrorMessage))
            return;

        _dialogs.Show(
            AppDialogKind.Error,
            _localization.Get("HistoryError"),
            entry.ErrorMessage,
            Application.Current.MainWindow);
    }

    [RelayCommand(CanExecute = nameof(CanOpenProductImage))]
    private void OpenProductImage(ProductPreviewItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.ImageUrl))
            return;

        ProductImagePreviewWindow.Show(item.ImageUrl, item.OfficialName, Application.Current.MainWindow);
    }

    private static bool CanOpenProductImage(ProductPreviewItem? item)
        => item is not null && !string.IsNullOrWhiteSpace(item.ImageUrl);

    public void PrepareHistoryContextMenu(PrintHistoryEntry entry, string? cellValue)
    {
        _historyContextEntry = entry;
        _historyContextCellValue = cellValue;
        NotifyHistoryContextCommands();
    }

    public void PrepareInboundHistoryCopy(string? cellValue)
    {
        _historyContextCellValue = cellValue;
        CopyHistoryCellCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCopyHistoryCell))]
    private void CopyHistoryCell()
    {
        if (string.IsNullOrWhiteSpace(_historyContextCellValue))
            return;

        Clipboard.SetText(_historyContextCellValue);
    }

    [RelayCommand(CanExecute = nameof(HasHistoryContextEntry))]
    private async Task SelectHistoryContextEntryAsync()
        => await SelectHistoryEntryAsync(_historyContextEntry);

    [RelayCommand(CanExecute = nameof(HasHistoryContextEntry))]
    private async Task ReprintHistoryContextEntryAsync()
        => await ReprintAsync(_historyContextEntry);

    [RelayCommand(CanExecute = nameof(HasHistoryContextEntry))]
    private async Task DeleteHistoryContextEntryAsync()
        => await DeleteHistoryEntryAsync(_historyContextEntry);

    private bool CanCopyHistoryCell() => !string.IsNullOrWhiteSpace(_historyContextCellValue);

    private bool HasHistoryContextEntry() => _historyContextEntry is not null;

    public void NotifyHistoryContextCommands()
    {
        CopyHistoryCellCommand.NotifyCanExecuteChanged();
        SelectHistoryContextEntryCommand.NotifyCanExecuteChanged();
        ReprintHistoryContextEntryCommand.NotifyCanExecuteChanged();
        DeleteHistoryContextEntryCommand.NotifyCanExecuteChanged();
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

    private async Task ReloadHistoryAsync(CancellationToken ct = default)
        => await RefreshHistoryAsync(ct);

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

            var suggestedName = $"LabelFlow_History_{HistoryDay:yyyyMMdd}.xlsx";
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

    [RelayCommand]
    private void ExportInboundHistoryToExcel()
    {
        try
        {
            var entries = InboundHistory.ToList();
            if (entries.Count == 0)
            {
                _dialogs.Show(
                    AppDialogKind.Warning,
                    _localization.Get("ModeSelect_ProductInboundTitle"),
                    _localization.Get("HistoryExportEmpty"));
                return;
            }

            var dayStamp = InboundHistoryDay.ToString("yyyyMMdd");
            var suggestedName = $"UrunGiris_Gecmis_{dayStamp}_{DateTime.Now:HHmm}.xlsx";
            var path = PromptSaveExcelFile?.Invoke(suggestedName);
            if (string.IsNullOrWhiteSpace(path))
                return;

            _historyExport.ExportInboundToExcel(entries, path, _localization);
            _dialogs.Show(
                AppDialogKind.Info,
                _localization.Get("HistoryExportTitle"),
                _localization.Get("HistoryExportSuccessAll", entries.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ürün girişi geçmişi Excel dışa aktarımı başarısız.");
            _dialogs.Show(AppDialogKind.Error, _localization.Get("HistoryExportTitle"), _localization.Get("HistoryExportFailed"));
        }
    }
}
