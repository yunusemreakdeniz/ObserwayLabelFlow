using System.Text.Json.Serialization;

namespace ObserwayLabelFlow.Core.Orders;

public sealed class OrderProductDto
{
    public string Asin { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Title { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("productTitle")]
    public string? ProductTitle { get; set; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("officialName")]
    public string? OfficialName { get; set; }

    public int Quantity { get; set; }
    public double Weight { get; set; }
    public double Length { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string? ImageUrl { get; set; }
    public string? Image { get; set; }
    public string? ProductImage { get; set; }
    public string? ThumbnailUrl { get; set; }

    public string? ResolveTitle()
    {
        foreach (var value in new[] { Title, Name, ProductTitle, ProductName, OfficialName })
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    public string? ResolveImageUrl()
    {
        if (!string.IsNullOrWhiteSpace(ImageUrl))
            return ImageUrl;
        if (!string.IsNullOrWhiteSpace(Image))
            return Image;
        if (!string.IsNullOrWhiteSpace(ProductImage))
            return ProductImage;
        if (!string.IsNullOrWhiteSpace(ThumbnailUrl))
            return ThumbnailUrl;
        return null;
    }
}
