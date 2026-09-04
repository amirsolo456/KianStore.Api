using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("DiscountCodeUsage")]
public sealed class DiscountCodeUsage
{
    public long Id { get; set; }
    public int DiscountCodeId { get; set; }
    public int PersonId { get; set; }
    public decimal OrderAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public int? IdSal { get; set; }
    public string? IdSanad { get; set; }
    public DateTime UsedAt { get; set; }
}
