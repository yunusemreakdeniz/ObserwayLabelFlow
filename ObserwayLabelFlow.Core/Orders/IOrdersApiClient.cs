using ObserwayLabelFlow.Core.Common;

namespace ObserwayLabelFlow.Core.Orders;

public interface IOrdersApiClient
{
    /// <summary>
    /// OBS sipariş numarası veya satın alma takip numarası ile sipariş getirir.
    /// </summary>
    Task<Result<OrderDto>> GetOrderByTrackingNumberAsync(string reference, CancellationToken ct = default);

    /// <summary>
    /// Yazdırma sonrası siparişi çıkışa hazır olarak işaretler (HTTP 204).
    /// </summary>
    Task<Result<bool>> MarkOutboundReadyAsync(string reference, CancellationToken ct = default);
}
