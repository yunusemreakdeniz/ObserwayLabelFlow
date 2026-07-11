namespace ObserwayLabelFlow.Core.History;

public sealed class HistoryOrderSnapshot
{
    public string AmazonOrderId { get; set; } = string.Empty;
    public string OrderSource { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string OrderCancelStatus { get; set; } = string.Empty;
    public string? PaymentType { get; set; }
    public string? ShippingPrice { get; set; }
    public string? CarrierService { get; set; }
    public HistoryCustomerSnapshot? Customer { get; set; }
    public List<HistoryProductSnapshot> Products { get; set; } = new();
}

public sealed class HistoryCustomerSnapshot
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string? District { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string State { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string? Contact { get; set; }
}

public sealed class HistoryProductSnapshot
{
    public string Asin { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int Quantity { get; set; }
    public double Weight { get; set; }
    public double Length { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string? ImageUrl { get; set; }
}
