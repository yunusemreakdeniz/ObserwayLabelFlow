using ObserwayLabelFlow.Core.Common;

namespace ObserwayLabelFlow.Core.Orders;

public interface IOrdersApiClient
{
    Task<Result<OrderDto>> GetOrderByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default);
}
