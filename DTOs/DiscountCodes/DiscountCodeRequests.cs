namespace KianStore.Api.DTOs.DiscountCodes;

public class CreateDiscountCodeRequest
{
    public string Code { get; set; } = null!;
    public string? Title { get; set; }
    public int Type { get; set; } = 1; // 1=percentage, 2=fixed amount
    public int Scope { get; set; } = 1; // 1=public, 2=private
    public int? PersonId { get; set; }
    public decimal Value { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? UsageLimit { get; set; }
    public int? PerCustomerLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

public sealed class UpdateDiscountCodeRequest : CreateDiscountCodeRequest
{
}

public sealed class ValidateDiscountCodeRequest
{
    public string Code { get; set; } = null!;
    public int PersonId { get; set; }
    public decimal OrderAmount { get; set; }
}

public sealed class IssueNextPurchaseDiscountRequest
{
    public int PersonId { get; set; }
    public int Type { get; set; } = 1;
    public decimal Value { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int? PerCustomerLimit { get; set; } = 1;
    public int ValidDays { get; set; } = 30;
    public string? Title { get; set; }
}
