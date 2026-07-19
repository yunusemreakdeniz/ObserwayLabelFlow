using System.Text.Json.Serialization;

namespace ObserwayLabelFlow.Core.Orders;

public sealed class OrderDto
{
    public string ObserwayOrderNumber { get; set; } = string.Empty;
    public string AmazonOrderId { get; set; } = string.Empty;
    public string OrderSource { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string OrderCancelStatus { get; set; } = string.Empty;
    public string? PaymentType { get; set; }
    public string? ShippingPrice { get; set; }
    public string? CarrierName { get; set; }
    public string? CarrierService { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset? InboundReceivedAt { get; set; }
    public DateTimeOffset? OutboundReadyAt { get; set; }
    public OrderCustomerDto? Customer { get; set; }
    public List<OrderProductDto> Products { get; set; } = new();

    [JsonPropertyName("orderProducts")]
    public List<OrderProductDto>? OrderProducts { get; set; }

    [JsonPropertyName("lineItems")]
    public List<OrderProductDto>? LineItems { get; set; }

    public List<OrderNoteDto> OrderNotes { get; set; } = new();

    public IReadOnlyList<OrderProductDto> GetProducts()
    {
        if (Products.Count > 0)
            return Products;
        if (OrderProducts is { Count: > 0 })
            return OrderProducts;
        if (LineItems is { Count: > 0 })
            return LineItems;

        return Products ?? OrderProducts ?? LineItems ?? [];
    }

    public string? ResolveLabelUri()
        => string.IsNullOrWhiteSpace(Label) ? null : Label.Trim();
}
