using System.ComponentModel.DataAnnotations;

namespace KianStore.Api.DTOs.Documents;

public sealed class UpdateSaleDocumentRequest
{
    [Required, MinLength(1)]
    public List<UpdateSaleDocumentItemRequest> Items { get; init; } = new();
}

public sealed class UpdateSaleDocumentItemRequest
{
    [Required, StringLength(20)] public string IdKala { get; init; } = null!;
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] public decimal Quantity { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal UnitPrice { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal PurchasePrice { get; init; }
    public bool IsIncoming { get; init; }
    [StringLength(200)] public string? Description { get; init; }
}
