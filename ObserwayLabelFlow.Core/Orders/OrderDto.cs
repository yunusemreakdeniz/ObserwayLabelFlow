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
    public OrderCustomerDto? Customer { get; set; }
    public List<OrderProductDto> Products { get; set; } = new();
    public List<OrderNoteDto> OrderNotes { get; set; } = new();

    public string? ResolveLabelUri()
        => string.IsNullOrWhiteSpace(Label) ? null : Label.Trim();
}
