namespace KianStore.Api.DTOs.Documents;

public sealed class DocumentResponse
{
    public int IdSal { get; init; }
    public string Id { get; init; } = null!;
    public int SanadType { get; init; }
    public int IdAnbar { get; init; }
    public int IdTaraf { get; init; }
    public int IdTarafType { get; init; }
    public int IdFaktor { get; init; }
    public string SabtDate { get; init; } = null!;
    public decimal TotalAmount { get; init; }
    public bool IsFinal { get; init; }
    public string? Description { get; init; }
    public string? TarafName { get; init; }
    public List<DocumentItemResponse> Items { get; init; } = new();
}

public sealed class DocumentItemResponse
{
    public int Id2 { get; init; }
    public string IdKala { get; init; } = null!;
    public double Quantity { get; init; }
    public bool IsIncoming { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalAmount { get; init; }
}
