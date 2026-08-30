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

    [Column("IDSanjesh")]
    public int IdSanjesh { get; set; }

    [Column("KalaType")]
    public int KalaType { get; set; }

    [Column("MabFrosh")]
    public decimal MabFrosh { get; set; }

    [Column("MabKharid")]
    public decimal MabKharid { get; set; }

    [Column("IsDisabled")]
    public bool IsDisabled { get; set; }

    [Column("IDAnbarFrosh")]
    public int IdAnbarFrosh { get; set; }

    [Column("MinCount")]
    public int MinCount { get; set; }

    [Column("IDSanjesh2")]
    public int IdSanjesh2 { get; set; }

    [Column("Quantity")]
    public double Quantity { get; set; }

    [Column("Barcode")]
    [StringLength(30)]
    public string Barcode { get; set; } = string.Empty;
}
