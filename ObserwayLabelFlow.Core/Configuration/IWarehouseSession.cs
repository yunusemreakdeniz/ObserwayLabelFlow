using ObserwayLabelFlow.Core.Warehouse;

namespace ObserwayLabelFlow.Core.Configuration;

public interface IWarehouseSession
{
    WarehouseDto? Selected { get; }

    IReadOnlyList<WarehouseDto> Available { get; }

    bool HasSelection { get; }

    long SelectedWarehouseId { get; }

    bool AllowInbound { get; }

    bool AllowOutbound { get; }

    bool ShowProductDetailsOnInbound { get; }

    string SelectedDisplayName { get; }

    event EventHandler? Changed;

    Task ReloadAvailableAsync(CancellationToken cancellationToken = default);

    Task SelectAsync(WarehouseDto warehouse, CancellationToken cancellationToken = default);

    Task ClearSelectionAsync(CancellationToken cancellationToken = default);
}
