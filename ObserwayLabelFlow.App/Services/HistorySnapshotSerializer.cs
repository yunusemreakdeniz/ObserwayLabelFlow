using System.Text.Json;
using ObserwayLabelFlow.Core.History;
using ObserwayLabelFlow.Core.Orders;

namespace ObserwayLabelFlow.App.Services;

internal static class HistorySnapshotSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(HistoryOrderSnapshot snapshot)
        => JsonSerializer.Serialize(snapshot, JsonOptions);

    public static HistoryOrderSnapshot? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<HistoryOrderSnapshot>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static HistoryOrderSnapshot FromOrder(OrderDto order)
        => new()
        {
            AmazonOrderId = order.AmazonOrderId,
            OrderSource = order.OrderSource,
            PaymentStatus = order.PaymentStatus,
            OrderCancelStatus = order.OrderCancelStatus,
            PaymentType = order.PaymentType,
            ShippingPrice = order.ShippingPrice,
            CarrierService = order.CarrierService,
            Customer = order.Customer is null
                ? null
                : new HistoryCustomerSnapshot
                {
                    FullName = order.Customer.FullName,
                    PhoneNumber = order.Customer.PhoneNumber,
                    CountryCode = order.Customer.CountryCode,
                    Country = order.Customer.Country,
                    City = order.Customer.City,
                    ZipCode = order.Customer.ZipCode,
                    District = order.Customer.District,
                    AddressLine1 = order.Customer.AddressLine1,
                    AddressLine2 = order.Customer.AddressLine2,
                    State = order.Customer.State,
                    StoreName = order.Customer.StoreName,
                    Contact = order.Customer.Contact,
                },
            Products = order.Products.Select(p => new HistoryProductSnapshot
            {
                Asin = p.Asin,
                Sku = p.Sku,
                Title = p.Title,
                Quantity = p.Quantity,
                Weight = p.Weight,
                Length = p.Length,
                Width = p.Width,
                Height = p.Height,
                ImageUrl = p.ResolveImageUrl(),
            }).ToList(),
        };
}
