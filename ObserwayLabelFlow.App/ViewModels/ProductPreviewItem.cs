using CommunityToolkit.Mvvm.ComponentModel;

namespace ObserwayLabelFlow.App.ViewModels;

/// <summary>Single product row for the left panel; empty strings show skeleton placeholders.</summary>
public sealed class ProductPreviewItem : ObservableObject
{
    private string? officialName;
    private string? size;
    private string? asin;
    private string? quantity;
    private string? sku;
    private string? imageUrl;

    public string? OfficialName
    {
        get => officialName;
        set => SetProperty(ref officialName, value);
    }

    public string? Size
    {
        get => size;
        set => SetProperty(ref size, value);
    }

    public string? Asin
    {
        get => asin;
        set => SetProperty(ref asin, value);
    }

    public string? Quantity
    {
        get => quantity;
        set => SetProperty(ref quantity, value);
    }

    public string? Sku
    {
        get => sku;
        set => SetProperty(ref sku, value);
    }

    public string? ImageUrl
    {
        get => imageUrl;
        set => SetProperty(ref imageUrl, value);
    }
}
