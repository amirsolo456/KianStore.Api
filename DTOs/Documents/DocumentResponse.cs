using System.Text.Json.Serialization;

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
    public decimal DiscountAmount { get; init; }
    public bool IsFinal { get; init; }
    public bool IsBookmarked { get; init; }
    public string? Description { get; init; }
    public string? TarafName { get; init; }
    public int? PurchaseEmployeeId { get; init; }
    public string? PurchaseEmployeeName { get; init; }
    [JsonPropertyName("smsStatus")]
    public string SmsStatus { get; init; } = "not_sent";
    public string[] ConsumedDiscountCodes { get; init; } = Array.Empty<string>();
    public string? IssuedNextPurchaseDiscountCode { get; init; }
    public bool NextPurchaseSmsSent { get; init; }
    public List<DocumentItemResponse> Items { get; init; } = new();
}

public sealed class DocumentBookmarkRequest
{
    public bool IsBookmarked { get; init; }
}

public sealed class DocumentBookmarkResponse
{
    public int IdSal { get; init; }
    public string Id { get; init; } = null!;
    public bool IsBookmarked { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class DocumentItemResponse
{
    public int Id2 { get; init; }
    public string IdKala { get; init; } = null!;
    public double Quantity { get; init; }
    public bool IsIncoming { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal PurchasePrice { get; init; }
    public decimal TotalAmount { get; init; }
}
