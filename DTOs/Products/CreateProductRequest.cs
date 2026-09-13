using System.ComponentModel.DataAnnotations;

namespace KianStore.Api.DTOs.Products;

public sealed class CreateProductRequest
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Name { get; set; } = null!;

    public int UnitId { get; set; } = 1;
    public int TypeId { get; set; } = 1;
    public decimal SalePrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public string? Barcode { get; set; }
}
