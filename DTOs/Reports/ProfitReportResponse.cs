namespace KianStore.Api.DTOs.Reports;

public sealed class ProfitReportResponse
{
    public string FromDate { get; init; } = null!;
    public string ToDate { get; init; } = null!;
    public decimal TotalSales { get; init; }
    public decimal TotalPurchaseCost { get; init; }
    public decimal TotalProfit { get; init; }
    public List<ProfitReportItemResponse> Items { get; init; } = new();
}

public sealed class ProfitReportItemResponse
{
    public string IdKala { get; init; } = null!;
    public double Quantity { get; init; }
    public decimal SalesAmount { get; init; }
    public decimal PurchaseCost { get; init; }
    public decimal Profit { get; init; }
}
