using System.Text.Json;
using ObserwayLabelFlow.Core.Orders;
using ObserwayLabelFlow.Infrastructure.Http;

namespace ObserwayLabelFlow.Infrastructure.Orders;

internal static class OrderDtoJson
{
    private static readonly string[] ProductArrayNames =
    [
        "products",
        "orderProducts",
        "lineItems",
        "items",
        "orderItems",
        "orderProductList"
    ];

    public static OrderDto? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var order = JsonSerializer.Deserialize<OrderDto>(json, HttpJson.DefaultOptions);
        if (order is null)
            return null;

        if (order.GetProducts().Count > 0)
            return order;

        var products = TryExtractProducts(root);
        if (products.Count > 0)
            order.Products = products;

        return order;
    }

    public static string DescribeProductShape(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var props = root.EnumerateObject().Select(p =>
            {
                var kind = p.Value.ValueKind.ToString();
                if (p.Value.ValueKind == JsonValueKind.Array)
                    kind = $"Array[{p.Value.GetArrayLength()}]";
                return $"{p.Name}:{kind}";
            });
            return string.Join(", ", props);
        }
        catch (Exception ex)
        {
            return $"shape-parse-failed: {ex.Message}";
        }
    }

    private static List<OrderProductDto> TryExtractProducts(JsonElement root)
    {
        foreach (var name in ProductArrayNames)
        {
            if (!TryGetPropertyIgnoreCase(root, name, out var array) || array.ValueKind != JsonValueKind.Array)
                continue;

            var list = DeserializeProducts(array);
            if (list.Count > 0)
                return list;
        }

        // Bazı yanıtlarda ürünler customer/order altında değil; kökteki ilk dizi ürün gibi görünebilir.
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array || prop.Value.GetArrayLength() == 0)
                continue;

            var first = prop.Value[0];
            if (first.ValueKind != JsonValueKind.Object)
                continue;

            if (!LooksLikeProductObject(first))
                continue;

            var list = DeserializeProducts(prop.Value);
            if (list.Count > 0)
                return list;
        }

        return [];
    }

    private static List<OrderProductDto> DeserializeProducts(JsonElement array)
    {
        try
        {
            return JsonSerializer.Deserialize<List<OrderProductDto>>(array.GetRawText(), HttpJson.DefaultOptions)
                   ?? [];
        }
        catch
        {
            var list = new List<OrderProductDto>();
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                try
                {
                    var product = JsonSerializer.Deserialize<OrderProductDto>(item.GetRawText(), HttpJson.DefaultOptions);
                    if (product is not null)
                        list.Add(product);
                }
                catch
                {
                    // Tek bozuk satır tüm listeyi düşürmesin.
                }
            }

            return list;
        }
    }

    private static bool LooksLikeProductObject(JsonElement obj)
    {
        foreach (var name in new[] { "asin", "sku", "title", "imageUrl", "quantity", "officialName", "productName" })
        {
            if (TryGetPropertyIgnoreCase(obj, name, out _))
                return true;
        }

        return false;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
