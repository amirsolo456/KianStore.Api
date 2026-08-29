using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("Anbar")]
public class Anbar
{
    [Column("ID")]
    public int Id { get; set; }

    [Column("AnabrName")]
    public string Name { get; set; } = null!;

    [Column("AnbarType")]
    public int AnbarType { get; set; }

    [Column("NoActive")]
    public bool NoActive { get; set; }

    [Column("MasoolAnbar")]
    public int MasoolAnbar { get; set; }

    [Column("ShomareshType")]
    public int ShomareshType { get; set; }

    [Column("AnbarAddr")]
    public string? Address { get; set; }

    [Column("IDMarket")]
    public int IdMarket { get; set; }
}
