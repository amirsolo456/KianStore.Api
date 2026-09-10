using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("KalaWeb")]
public class KalaWeb
{
    [Key]
    [Column("IDKala")]
    [StringLength(20)]
    public string IdKala { get; set; } = null!;

    [Column("Slug")]
    [StringLength(200)]
    public string? Slug { get; set; }

    [Column("ShortDescription")]
    [StringLength(500)]
    public string? ShortDescription { get; set; }

    [Column("Description")]
    public string? Description { get; set; }

    [Column("MainImageUrl")]
    [StringLength(1000)]
    public string? MainImageUrl { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("DisplayOrder")]
    public int DisplayOrder { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Kala Kala { get; set; } = null!;
    public ICollection<KalaWebImage> Images { get; set; } = new List<KalaWebImage>();
}
