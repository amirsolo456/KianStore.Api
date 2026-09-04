using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("Takhfif")]
public sealed class Takhfif
{
    [Column("ID")]
    public int Id { get; set; }

    public string TakhfifName { get; set; } = null!;
    public double TakhfifDarsad { get; set; }
    public decimal ToMab1 { get; set; }
    public int SumType { get; set; }
    public int ByTakhfifKala { get; set; }
    public bool Pelekani { get; set; }
    public int IdHyperMarket { get; set; }
    public int IdKalaListEx { get; set; }
    public int IdKalaListOnly { get; set; }
    public int ApplyType { get; set; }
    public int TasviehType { get; set; }
    public bool IsDisabe { get; set; }
    public int IdUser { get; set; }
    public int OrderIndex { get; set; }
}
