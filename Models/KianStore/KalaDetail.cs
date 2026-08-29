using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("KalaDetail")]
public class KalaDetail
{
    [Column("IDKala")]
    public string IdKala { get; set; } = null!;

    [Column("IDAnbar")]
    public int IdAnbar { get; set; }

    [Column("Quantity")]
    public double Quantity { get; set; }

    [Column("LastMabKharid")]
    public decimal? LastMabKharid { get; set; }

    [Column("MabFrosh")]
    public decimal? MabFrosh { get; set; }

    [Column("MabFrosh1")]
    public decimal? MabFrosh1 { get; set; }

    [Column("lastChanged")]
    public byte[]? LastChanged { get; set; }
}
