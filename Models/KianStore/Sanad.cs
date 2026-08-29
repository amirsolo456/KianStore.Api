using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("Sanad")]
public class Sanad
{
    [Key]
    [Column("ID")]
    public long Id { get; set; }

    [Column("IDTaraf")]
    public int IdTaraf { get; set; }

    [Column("IDTarafType")]
    public int IdTarafType { get; set; }

    [Column("SanadType")]
    public int SanadType { get; set; }

    [Column("SabtDate")]
    [StringLength(10)]
    public string SabtDate { get; set; } = null!;

    [Column("IDAnbar")]
    public int IdAnbar { get; set; }

    [Column("IDMasool")]
    public int IdMasool { get; set; }

    [Column("IDFaktor")]
    public int IdFaktor { get; set; }

    [Column("SanadSal")]
    public int SanadSal { get; set; }

    [Column("Tozi")]
    public string? Description { get; set; }

    [Column("TotalMab")]
    public decimal TotalAmount { get; set; }
}

[Table("SanadDetail")]
public class SanadDetail
{
    [Column("ID")]
    public long Id { get; set; }

    [Column("IDKala")]
    [StringLength(20)]
    public string IdKala { get; set; } = null!;

    [Column("Meghdar")]
    public decimal Quantity { get; set; }

    [Column("Mab")]
    public decimal UnitPrice { get; set; }

    [Column("MabKol")]
    public decimal TotalPrice { get; set; }
}
