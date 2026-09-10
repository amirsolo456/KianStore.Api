using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("MobileOrderItems")]
public sealed class MobileOrderItem
{
    [Key]
    public long Id { get; set; }

    public long OrderId { get; set; }

    [StringLength(20)]
    public string KalaId { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public MobileOrder Order { get; set; } = null!;
}
