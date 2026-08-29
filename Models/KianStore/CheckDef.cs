using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("CheckDef")]
public sealed class CheckDef
{
    [Column("ID")]
    public int Id { get; set; }

    [Column("Type")]
    public int Type { get; set; }

    [Column("HesType")]
    public int HesType { get; set; }

    [Column("Bank")]
    public string Bank { get; set; } = null!;

    [Column("Shobeh")]
    public string Shobeh { get; set; } = null!;

    [Column("HesabNum")]
    public string? HesabNum { get; set; }

    [Column("Mojodi")]
    public decimal Mojodi { get; set; }

    [Column("Shahr")]
    public string? Shahr { get; set; }

    [Column("HesName")]
    public string HesName { get; set; } = null!;

    [Column("ShobehNum")]
    public string? ShobehNum { get; set; }

    [Column("SahebHes")]
    public string? SahebHes { get; set; }

    [Column("Des")]
    public string? Des { get; set; }

    [Column("IDUser")]
    public int IdUser { get; set; }

    [Column("IsSelect")]
    public bool IsSelect { get; set; }

    [Column("IDHyperMarket")]
    public int IdHyperMarket { get; set; }
}
