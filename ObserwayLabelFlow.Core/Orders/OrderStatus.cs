namespace ObserwayLabelFlow.Core.Orders;

/// <summary>
/// Backend OrderStatus değerleri. Görünen metin istemci lokalizasyonundan gelir;
/// API'deki Display attribute metinleri dile bağlı değildir.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Received = 1,
    Processing = 2,
    LabelCreated = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    Returned = 7
}
