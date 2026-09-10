using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("MobileOrderPayments")]
public sealed class MobileOrderPayment
{
    [Key]
    public long Id { get; set; }

    public long OrderId { get; set; }

    [StringLength(20)]
    public string? PaymentDate { get; set; }

    public decimal Amount { get; set; }

    [StringLength(50)]
    public string? TrackingNumber { get; set; }

    [StringLength(50)]
    public string? BankName { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }

    public MobileOrder Order { get; set; } = null!;
}
