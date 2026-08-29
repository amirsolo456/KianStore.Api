using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("Kala")]
public class Kala
{
    [Key]
    [Column("ID")]
    [StringLength(20)]
    public string Id { get; set; } = null!;

    [Column("KalaName")]
    [StringLength(50)]
    public string KalaName { get; set; } = null!;

    [Column("MabFrosh")]
    public decimal MabFrosh { get; set; }

    [Column("MabKharid")]
    public decimal MabKharid { get; set; }

    [Column("IsDisabled")]
    public bool IsDisabled { get; set; }
}