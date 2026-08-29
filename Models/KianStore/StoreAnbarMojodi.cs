namespace KianStore.Api.Models.KianStore;

public sealed class StoreAnbarMojodi
{
    public int IDSal { get; set; }

    public int IDAnbar { get; set; }

    public string IDKala { get; set; } = null!;

    public string KalaName { get; set; } = null!;

    public decimal Mojoodi { get; set; }
}