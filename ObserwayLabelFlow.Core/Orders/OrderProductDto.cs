namespace ObserwayLabelFlow.Core.Orders;

public sealed class OrderProductDto
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
    public string? Image { get; set; }
    public string? ProductImage { get; set; }
    public string? ThumbnailUrl { get; set; }

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
