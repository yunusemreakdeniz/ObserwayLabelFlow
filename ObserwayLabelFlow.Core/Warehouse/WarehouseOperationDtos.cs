namespace ObserwayLabelFlow.Core.Warehouse;

public sealed record WarehouseInboundRequest(
    long WarehouseId,
    string Reference,
    long? OrderId = null,
    string? InboundTrackingNumber = null,
    string? InboundCarrier = null,
    string? Note = null);

public sealed record WarehouseInboundResult(
    long OperationId,
    long WarehouseId,
    string ScannedReference,
    bool Matched,
    long? OrderId,
    string? OrderNumber,
    string? MatchStatus,
    DateTimeOffset? InboundReceivedAt,
    bool AlreadyExists = false);

public sealed record WarehouseOutboundRequest(
    long WarehouseId,
    string Reference,
    long? OrderId = null,
    string? VehicleName = null,
    string? Note = null);

public sealed record WarehouseOutboundResult(
    long OperationId,
    long WarehouseId,
    string ScannedReference,
    bool Matched,
    long? OrderId,
    string? OrderNumber,
    string? MatchStatus,
    string? LabelUrl,
    DateTimeOffset? OutboundReadyAt,
    bool AlreadyExists = false,
    string? VehicleName = null);
