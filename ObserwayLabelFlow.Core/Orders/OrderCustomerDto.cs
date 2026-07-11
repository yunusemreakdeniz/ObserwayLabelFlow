namespace ObserwayLabelFlow.Core.Orders;

public sealed class OrderCustomerDto
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
