using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("DiscountCode")]
public sealed class DiscountCode
{
    [Column("Id")]
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Title { get; set; }
    public int TakhfifId { get; set; }
    public int Type { get; set; } // 1=percentage, 2=fixed amount
    public int Scope { get; set; } // 1=public, 2=private
    public int? PersonId { get; set; }
    public decimal Value { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public int? PerCustomerLimit { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
