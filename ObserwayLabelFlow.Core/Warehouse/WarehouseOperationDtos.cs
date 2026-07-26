namespace ObserwayLabelFlow.Core.Warehouse;

public sealed record WarehouseInboundRequest(
    long WarehouseId,
    string? InboundTrackingNumber = null,
    string? InboundCarrier = null,
    string? Note = null);

public sealed record WarehouseInboundResult(
    long OrderId,
    string OrderNumber,
    long OperationId,
    DateTimeOffset? InboundReceivedAt);

public sealed record WarehouseLoadToVehicleRequest(
    long WarehouseId,
    string VehicleName,
    string? Note = null);

public sealed record WarehouseLoadToVehicleResult(
    long OrderId,
    string OrderNumber,
    long OperationId,
    string VehicleName,
    string? CarrierName,
    string? CarrierCode);

public sealed record WarehouseOutboundRequest(
    long WarehouseId,
    string? Note = null);

public sealed record WarehouseOutboundResult(
    long OrderId,
    string OrderNumber,
    long OperationId,
    DateTimeOffset? OutboundReadyAt);
