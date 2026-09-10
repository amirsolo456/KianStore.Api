using System.ComponentModel.DataAnnotations;

namespace KianStore.Api.DTOs.WebOrders;

public sealed class CreateWebOrderRequest
{
    [Required, StringLength(120)] public string Name { get; init; } = null!;
    [Required, StringLength(20)] public string Mobile { get; init; } = null!;
    [StringLength(50)] public string? Phone { get; init; }
    [StringLength(500)] public string? Address { get; init; }
    [StringLength(700)] public string? Description { get; init; }
    [MinLength(1)] public List<CreateWebOrderItem> Items { get; init; } = new();
}

public sealed class CreateWebOrderItem
{
    [Required, StringLength(20)] public string IdKala { get; init; } = null!;
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] public decimal Quantity { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal? UnitPrice { get; init; }
}

public sealed class WebOrderCreatedResponse
{
    public int IdSal { get; init; }
    public string Id { get; init; } = null!;
    public int IdFaktor { get; init; }
    public int IdTaraf { get; init; }
    public decimal TotalAmount { get; init; }
}
