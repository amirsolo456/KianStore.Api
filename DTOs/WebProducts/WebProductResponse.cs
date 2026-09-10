namespace KianStore.Api.DTOs.WebProducts;

public sealed class WebProductResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Stock { get; set; }
    public string? Barcode { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? MainImageUrl { get; set; }
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();
}
