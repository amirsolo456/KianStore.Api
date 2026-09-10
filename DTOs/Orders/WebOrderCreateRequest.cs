using System.ComponentModel.DataAnnotations;

namespace KianStore.Api.DTOs.Orders;

public sealed class WebOrderCreateRequest
{
    [Required, StringLength(20)]
    public string Mobile { get; set; } = null!;

    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }

    public string? Address { get; set; }
    public int? TarafId { get; set; }
    public int? TarafType { get; set; }
    public int? IdSal { get; set; }
    public int? IdAnbar { get; set; }
    public string? Notes { get; set; }
    public List<WebOrderItemRequest> Items { get; set; } = new();
}

public sealed class WebOrderItemRequest
{
    [Required, StringLength(20)]
    public string KalaId { get; set; } = null!;

    [Range(typeof(decimal), "0.001", "1000000")]
    public decimal Quantity { get; set; }
}
