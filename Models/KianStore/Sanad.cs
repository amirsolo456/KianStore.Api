using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("Sanad")]
public class Sanad
{
    public int IdSal { get; set; }
    public string Id { get; set; } = null!;
    public int SanadType { get; set; }
    public int IdAnbar { get; set; }
    public int IdTaraf { get; set; }
    public int IdTarafType { get; set; }
    public int IdFaktor { get; set; }
    public int IdTypeMab { get; set; }
    public decimal Takhfif { get; set; }
    public decimal MabDarSad { get; set; }
    public decimal MabKol { get; set; }
    public decimal MabNaghd { get; set; }
    public decimal MabFrosh { get; set; }
    public string SabtDate { get; set; } = null!;
    public decimal MabCheck { get; set; }
    public decimal MabBed { get; set; }
    public int IdMasool { get; set; }
    public int? IdTaiid { get; set; }
    public string? Des { get; set; }
    public int? IDEijad { get; set; }
    public int? IdDoreh { get; set; }
    public float CountGhest { get; set; }
    public float DarsadGhest { get; set; }
    public decimal Maliat1 { get; set; }
    public float Maliat1Darsad { get; set; }
    public bool Maliat1Sel { get; set; }
    public decimal Maliat2 { get; set; }
    public float Maliat2Darsad { get; set; }
    public bool Maliat2Sel { get; set; }
    public decimal MabHarGhest { get; set; }
    public decimal MabKolAghsat { get; set; }
    public bool GhestSel { get; set; }
    public int KarmozdFrosh { get; set; }
    public string? TarafName2 { get; set; }
    public string? Sharh { get; set; }
    public decimal Takhfif2 { get; set; }
    public bool IsTasvieh { get; set; }
    public int TasviehID { get; set; }
    public bool Disable { get; set; }
    public int IDSanadEx { get; set; }
    public int IDSanadEx2 { get; set; }
    public int IDSanadEx3 { get; set; }
    public bool ShowInSanad { get; set; }
    public bool ShowInFaktor { get; set; }
    public string TasviehDate { get; set; } = null!;
    public bool IsTasviehDate { get; set; }
    public int IDSandogh { get; set; }
    public int IDSandoghType { get; set; }
    public decimal MabKart { get; set; }
    public decimal MabFish { get; set; }
    public int IDKart { get; set; }
    public int IDTypeKart { get; set; }
    public bool IsFinal { get; set; }
    public int IDFroshMabType { get; set; }
    public string? SanadTime { get; set; }
    public bool TakhfifKala1 { get; set; }
    public bool TakhfifKala2 { get; set; }
    public bool TakhfifKala3 { get; set; }
    public bool IsMaliat1Darsad { get; set; }
    public bool IsMaliat1Kala { get; set; }
    public bool IsMaliat2Darsad { get; set; }
    public bool IsMaliat2Kala { get; set; }
    public bool IsPorsant { get; set; }
    public bool IsPorsantMabKol { get; set; }
    public bool IsPorsantMabKala { get; set; }
    public decimal HazFaktor { get; set; }
    public int IDHazFaktor { get; set; }
    public decimal HazFaktor2 { get; set; }
    public int IDHazFaktor2 { get; set; }
    public double TakhfifDarsad { get; set; }
    public string? TakhfifOnvan { get; set; }
    public decimal MabEzaf { get; set; }
    public float MabEzafDarsad { get; set; }
    public string? MabEzafOnvan { get; set; }
    public string? SefareshID { get; set; }
    public bool IsSavedFinal { get; set; }
    public int IDSanad { get; set; }
    public decimal Takhfif3 { get; set; }
    public decimal TakhfifKala { get; set; }
    public int IDFish { get; set; }
    public int IDFoodMahal { get; set; }
    public string? Tel { get; set; }
    public string? Add { get; set; }
    public string? CodeMeli { get; set; }
    public int? Miz { get; set; }
    public double GpsLat { get; set; }
    public double GpsLong { get; set; }
    public int TasvieType { get; set; }
    public int TasvieCheck { get; set; }
    public int IDAnbar2 { get; set; }
    public int IDTaraf2 { get; set; }
    public int HMarketID { get; set; }
    public string IDRef { get; set; } = null!;
    public int SanadTypeRef { get; set; }
    public string IDRefRecive { get; set; } = null!;
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public byte[]? LastChange { get; set; }
    public decimal? FroshArzesh { get; set; }
    public decimal? TakhfifKalaArzesh { get; set; }
    public decimal? HMaliat1 { get; set; }
    public decimal? HMaliat2 { get; set; }
    public string? TejaratCode { get; set; }
    public int StateMaliat { get; set; }
    public string? SabtDateOrg { get; set; }
    public decimal MabBonKart { get; set; }
    public decimal MabBonKartTakhfif { get; set; }
    public decimal Takhfif1 { get; set; }
    public int IDTarafTahator { get; set; }
    public int? TasviehRozSum { get; set; }
    public int? IDSanadAtf { get; set; }
    public decimal? MabFroshCalNaghd { get; set; }
    public decimal? MabCalNaghd { get; set; }
    public decimal? MabKarMozd { get; set; }
    public decimal? MabTahator { get; set; }
    public decimal? SumMabEzafatMoaf { get; set; }
    public int? IDState { get; set; }
    public string? CodeMaliat { get; set; }
    public bool? IsTasviehFaktor { get; set; }
    public decimal? TasviehMab { get; set; }
}

[Table("SanadDetail")]
public class SanadDetail
{
    public int IdSal { get; set; }
    public string IdSanad { get; set; } = null!;
    public int Id2 { get; set; }
    public string? AtfNum { get; set; }
    public string IdKala { get; set; } = null!;
    public double Bed { get; set; }
    public double Bes { get; set; }
    public decimal BedMab { get; set; }
    public decimal BesMab { get; set; }
    public string? Des { get; set; }
    public decimal SumMab { get; set; }
    public int IdAnbar { get; set; }
    public decimal IdKalaType { get; set; }
    public decimal BedMabKharid { get; set; }
    public decimal Maliat { get; set; }
    public bool Maliat1 { get; set; }
    public bool Maliat2 { get; set; }
    public double TakhfifDarsad { get; set; }
    public float PorsantDarsad { get; set; }
    public decimal HazKala { get; set; }
    public decimal HazKalaKharid { get; set; }
    public int IdSanjesh { get; set; }
    public int IdSanjesh2 { get; set; }
    public double BedBesZarib { get; set; }
    public int SanadType { get; set; }
    public int? PropKala { get; set; }
    public int? PropKala2 { get; set; }
    public string? Des1 { get; set; }
    public string? Des2 { get; set; }
    public string? Des3 { get; set; }
    public double? SumBed { get; set; }
    public double? SumBes { get; set; }
    public decimal? HazKala2 { get; set; }
    public decimal? HazKala3 { get; set; }
    public decimal SumTakhfifKala { get; set; }
    public decimal? HazKala1 { get; set; }
    public decimal? HazKalaGift1 { get; set; }
    public decimal? HazKalaGift2 { get; set; }
    public decimal? HazKalaGift3 { get; set; }
    public string IdAttribValuesStock { get; set; } = null!;
    public double? TakhfifD2 { get; set; }
    public double? TakhfifD3 { get; set; }
    public decimal? TakhfifMab1 { get; set; }
    public decimal? TakhfifMab2 { get; set; }
    public double? MaliatD1 { get; set; }
    public double? MaliatD2 { get; set; }
    public int? TasviehRoz { get; set; }
    public decimal? MaliatMab1 { get; set; }
    public decimal? MaliatMab2 { get; set; }
    public decimal? SumMabTakh { get; set; }
    public decimal? SumMabMaliat { get; set; }
    public decimal? MabFroshByTakh { get; set; }
    public double Bed2 { get; set; }
    public double Bes2 { get; set; }
    public decimal BedMab2 { get; set; }
    public decimal BesMab2 { get; set; }
    public decimal? MabEzafatMoaf { get; set; }
}
