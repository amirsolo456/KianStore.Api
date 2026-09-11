using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/reports/profit")]
public sealed class ProfitReportsController : ControllerBase
{
    private readonly KianStoreDbContext _context;

    public ProfitReportsController(KianStoreDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ProfitReportResponse>>> Get(
        [FromQuery] int idSal,
        [FromQuery] string fromDate,
        [FromQuery] string toDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            return BadRequest(ApiResponse<ProfitReportResponse>.ErrorResult(
                "INVALID_DATE_RANGE",
                "fromDate و toDate الزامی هستند."));

        if (string.CompareOrdinal(fromDate, toDate) > 0)
            return BadRequest(ApiResponse<ProfitReportResponse>.ErrorResult(
                "INVALID_DATE_RANGE",
                "تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد."));

        // Profit is intentionally calculated only from sales invoices.
        // The captured purchase price stored on each sales line is the cost basis.
        var details = await _context.SanadDetails.AsNoTracking()
            .Where(d => d.IdSal == idSal && d.SanadType == 12 && d.Bes2 > 0)
            .Join(
                _context.Sanads.AsNoTracking(),
                d => new { d.IdSal, Id = d.IdSanad },
                s => new { s.IdSal, Id = s.Id },
                (d, s) => new { Detail = d, Sanad = s })
            .Where(x =>
                !x.Sanad.Disable &&
                string.CompareOrdinal(x.Sanad.SabtDate, fromDate) >= 0 &&
                string.CompareOrdinal(x.Sanad.SabtDate, toDate) <= 0)
            .Select(x => new
            {
                x.Detail.IdKala,
                Quantity = x.Detail.Bes2,
                SalesAmount = x.Detail.SumMab,
                PurchaseCost = (decimal)x.Detail.Bes2 * x.Detail.BedMabKharid
            })
            .ToListAsync(cancellationToken);

        var items = details
            .GroupBy(x => x.IdKala)
            .Select(g => new ProfitReportItemResponse
            {
                IdKala = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                SalesAmount = g.Sum(x => x.SalesAmount),
                PurchaseCost = g.Sum(x => x.PurchaseCost),
                Profit = g.Sum(x => x.SalesAmount - x.PurchaseCost)
            })
            .OrderByDescending(x => x.Profit)
            .ToList();

        var report = new ProfitReportResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalSales = items.Sum(x => x.SalesAmount),
            TotalPurchaseCost = items.Sum(x => x.PurchaseCost),
            TotalProfit = items.Sum(x => x.Profit),
            Items = items
        };

        return Ok(ApiResponse<ProfitReportResponse>.SuccessResult(
            report,
            message: "گزارش سود با موفقیت محاسبه شد."));
    }
}
