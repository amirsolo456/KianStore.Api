using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("KalaWebImage")]
public class KalaWebImage
{
    [Key]
    [Column("ID")]
    public long Id { get; set; }

    [Column("IDKala")]
    [StringLength(20)]
    public string IdKala { get; set; } = null!;

    [Column("ImageUrl")]
    [StringLength(1000)]
    public string ImageUrl { get; set; } = null!;

    [Column("AltText")]
    [StringLength(300)]
    public string? AltText { get; set; }

    [Column("DisplayOrder")]
    public int DisplayOrder { get; set; }

    [Column("IsMain")]
    public bool IsMain { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public KalaWeb KalaWeb { get; set; } = null!;
}
