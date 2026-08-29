using System.ComponentModel.DataAnnotations;

namespace KianStore.Api.DTOs.Orders;

public class AddPaymentRequest
{
    [Required]
    [StringLength(20)]
    public string PaymentDate { get; set; } = null!;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [StringLength(50)]
    public string? TrackingNumber { get; set; }

    [StringLength(50)]
    public string? BankName { get; set; }

    public string? Notes { get; set; }
}
