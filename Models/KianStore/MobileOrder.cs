using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("MobileOrders")]
public sealed class MobileOrder
{
    [Key]
    public long Id { get; set; }

    [Column("OrderNumber")]
    [StringLength(20)]
    public string OrderNumber { get; set; } = null!;

    [Column("FirstName")]
    [StringLength(50)]
    public string? FirstName { get; set; }

    [Column("LastName")]
    [StringLength(50)]
    public string? LastName { get; set; }

    [Column("Mobile")]
    [StringLength(20)]
    public string Mobile { get; set; } = null!;

    [Column("Address")]
    public string? Address { get; set; }

    [Column("PaymentDate")]
    [StringLength(20)]
    public string? PaymentDate { get; set; }

    [Column("PaymentAmount")]
    public decimal PaymentAmount { get; set; }

    [Column("Status")]
    public int Status { get; set; } = 1;

    [Column("TarafId")]
    public int? TarafId { get; set; }

    [Column("TarafType")]
    public int? TarafType { get; set; }

    [Column("SanadId")]
    [StringLength(20)]
    public string? SanadId { get; set; }

    [Column("SanadSal")]
    public int? SanadSal { get; set; }

    [Column("Notes")]
    public string? Notes { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("CreatedBy")]
    public int CreatedBy { get; set; }

    public ICollection<MobileOrderItem> Items { get; set; } = new List<MobileOrderItem>();
    public ICollection<MobileOrderPayment> Payments { get; set; } = new List<MobileOrderPayment>();
}
