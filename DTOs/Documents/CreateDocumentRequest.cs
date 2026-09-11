using System.ComponentModel.DataAnnotations;
using KianStore.Api.DTOs.DiscountCodes;

namespace KianStore.Api.DTOs.Documents;

public sealed class CreateDocumentRequest
{
    [Range(1, int.MaxValue)] public int IdSal { get; init; } = 1405;
    [Range(1, int.MaxValue)] public int SanadType { get; init; }
    [Range(0, int.MaxValue)] public int IdAnbar { get; init; } = 1;
    [Range(1, int.MaxValue)] public int IdTaraf { get; init; }
    [Range(0, int.MaxValue)] public int IdTarafType { get; init; } = 2;
    [Range(1, int.MaxValue)] public int IdMasool { get; init; } = 101;
    [Range(1, int.MaxValue)] public int? IdFaktor { get; init; }
    [Range(0, int.MaxValue)] public int IdSandogh { get; init; }
    [Range(0, int.MaxValue)] public int IdSandoghType { get; init; }
    [Required, StringLength(10)] public string SabtDate { get; init; } = null!;
    [StringLength(200)] public string? Des { get; init; }
    [StringLength(700)] public string? Sharh { get; init; }
    public bool CheckStock { get; init; } = true;
    // Website orders can be created as pending documents. Pending documents are
    // visible to staff for review and must not affect stock until finalized.
    public bool IsPending { get; init; }
    [StringLength(50)] public string? SefareshID { get; init; }

    public List<ApplyDiscountCodeRequest> DiscountCodes { get; init; } = new();
    public IssueNextPurchaseDiscountRequest? NextPurchaseDiscount { get; init; }

    [MinLength(1)] public List<CreateDocumentItemRequest> Items { get; init; } = new();
}

public sealed class CreateDocumentItemRequest
{
    [Required, StringLength(20)] public string IdKala { get; init; } = null!;
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] public decimal Quantity { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal? UnitPrice { get; init; }
    // قیمت خریدِ قطعی همین قلم در زمان ثبت فروش. در صورت null، قیمت خرید فعلی کالا حفظ می‌شود.
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal? PurchasePrice { get; init; }
    public bool IsIncoming { get; init; }
    [StringLength(200)] public string? Description { get; init; }
}

public sealed class ApplyDiscountCodeRequest
{
    [Required, StringLength(50)] public string Code { get; init; } = null!;
}
