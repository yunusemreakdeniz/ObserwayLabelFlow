using ObserwayLabelFlow.Core.Common;

namespace ObserwayLabelFlow.Core.Warehouse;

public interface IWarehouseApiClient
{
    Task<Result<IReadOnlyList<WarehouseDto>>> GetMyWarehousesAsync(CancellationToken ct = default);

    Task<Result<WarehouseLookupDto>> LookupAsync(
        string reference,
        long warehouseId,
        CancellationToken ct = default);

    Task<Result<WarehouseInboundResult>> PostInboundAsync(
        WarehouseInboundRequest request,
        CancellationToken ct = default);

    Task<Result<WarehouseOutboundResult>> PostOutboundAsync(
        WarehouseOutboundRequest request,
        CancellationToken ct = default);
}
