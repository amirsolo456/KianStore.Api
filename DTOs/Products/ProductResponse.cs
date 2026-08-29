namespace KianStore.Api.DTOs.Products;

public class ProductResponse
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
}
