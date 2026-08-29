using System.ComponentModel.DataAnnotations;

namespace KianStore.Api.DTOs.Orders;

public class CreateOrderRequest
{
    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = null!;

    [Required]
    [StringLength(20)]
    public string Mobile { get; set; } = null!;

    public string? Address { get; set; }

    public string? PaymentDate { get; set; }
    public decimal? PaymentAmount { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Required]
    public string KalaId { get; set; } = null!;

    [Required]
    [Range(0.001, double.MaxValue)]
    public decimal Quantity { get; set; }
}
