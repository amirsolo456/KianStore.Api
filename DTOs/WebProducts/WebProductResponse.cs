namespace KianStore.Api.DTOs.WebProducts;

public sealed class WebProductResponse
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Stock { get; init; }
    public string? Barcode { get; init; }
    public bool IsActive { get; init; }
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public string? MainImageUrl { get; init; }
    public IReadOnlyList<string> ImageUrls { get; init; } = Array.Empty<string>();
}
