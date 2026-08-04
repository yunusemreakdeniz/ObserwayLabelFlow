using ObserwayLabelFlow.Core.Orders;
using ObserwayLabelFlow.Core.Warehouse;

namespace ObserwayLabelFlow.App.Services;

internal static class OrderStatusLocalizer
{
    public static string GetDisplay(ILocalizationService localization, int statusCode)
    {
        var key = statusCode switch
        {
            (int)OrderStatus.Pending => "OrderStatus_Pending",
            (int)OrderStatus.Received => "OrderStatus_Received",
            (int)OrderStatus.Processing => "OrderStatus_Processing",
            (int)OrderStatus.LabelCreated => "OrderStatus_LabelCreated",
            (int)OrderStatus.Shipped => "OrderStatus_Shipped",
            (int)OrderStatus.Delivered => "OrderStatus_Delivered",
            (int)OrderStatus.Cancelled => "OrderStatus_Cancelled",
            (int)OrderStatus.Returned => "OrderStatus_Returned",
            _ => null
        };

        return key is null
            ? localization.Get("OrderStatus_Unknown", statusCode)
            : localization.Get(key);
    }

    public static string GetDisplay(ILocalizationService localization, WarehouseLookupDto order)
    {
        if (order.OrderStatus is int statusCode)
            return GetDisplay(localization, statusCode);

        return string.IsNullOrWhiteSpace(order.OrderStatusDisplay)
            ? string.Empty
            : order.OrderStatusDisplay.Trim();
    }
}
