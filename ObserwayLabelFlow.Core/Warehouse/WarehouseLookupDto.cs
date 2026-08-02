using OrderStatusCode = ObserwayLabelFlow.Core.Orders.OrderStatus;

namespace ObserwayLabelFlow.Core.Warehouse;

public sealed class WarehouseLookupDto
{
    public bool Matched { get; set; }
    public long WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public bool ShowProductDetailsOnInbound { get; set; }
    public bool AllowInbound { get; set; }
    public bool AllowOutbound { get; set; }
    public long? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public int OrderStatus { get; set; }
    public string OrderStatusDisplay { get; set; } = string.Empty;
    public bool CanLoadToVehicle { get; set; }
    public string? BlockReason { get; set; }
    public string? CarrierName { get; set; }
    public string? CarrierCode { get; set; }
    public string? LabelUrl { get; set; }
    public DateTimeOffset? InboundReceivedAt { get; set; }
    public DateTimeOffset? OutboundReadyAt { get; set; }
    public bool AlreadyLoadedToVehicle { get; set; }
    public string? LastVehicleName { get; set; }
    public List<WarehouseProductDto> Products { get; set; } = new();

    public bool IsCancelledOrReturned
        => Matched
           && (OrderStatus is (int)OrderStatusCode.Cancelled or (int)OrderStatusCode.Returned
               || MatchesCancelOrReturn(OrderStatusDisplay)
               || MatchesCancelOrReturn(BlockReason));

    private static bool MatchesCancelOrReturn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var value = text.Trim();
        return Contains(value, "cancel")
               || Contains(value, "cancelled")
               || Contains(value, "canceled")
               || Contains(value, "iptal")
               || Contains(value, "return")
               || Contains(value, "returned")
               || Contains(value, "iade");
    }

    private static bool Contains(string haystack, string needle)
        => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}

public sealed class WarehouseProductDto
{
    public string Asin { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }
    public double? Length { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public double? Weight { get; set; }
}
