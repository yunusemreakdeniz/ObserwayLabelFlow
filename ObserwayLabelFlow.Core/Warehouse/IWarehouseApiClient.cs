using ObserwayLabelFlow.Core.Common;

namespace ObserwayLabelFlow.Core.Warehouse;

public interface IWarehouseApiClient
{
    Task<Result<WarehouseLookupDto>> LookupAsync(string reference, CancellationToken ct = default);

    Task<Result<WarehouseInboundResult>> MarkInboundReceivedAsync(
        long orderId,
        WarehouseInboundRequest request,
        CancellationToken ct = default);

    Task<Result<WarehouseLoadToVehicleResult>> LoadToVehicleAsync(
        long orderId,
        WarehouseLoadToVehicleRequest request,
        CancellationToken ct = default);

    Task<Result<WarehouseOutboundResult>> MarkOutboundReadyAsync(
        long orderId,
        WarehouseOutboundRequest request,
        CancellationToken ct = default);
}
