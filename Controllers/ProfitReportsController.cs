using KianStore.Api.DTOs.Reports;
using KianStore.Api.Data;
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
    public async Task<ActionResult<ProfitReportResponse>> Get(
        [FromQuery] int idSal,
        [FromQuery] string fromDate,
        [FromQuery] string toDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            return BadRequest("fromDate و toDate الزامی هستند.");

        var details = await _context.SanadDetails.AsNoTracking()
            .Where(d => d.IdSal == idSal && d.SanadType == 12 && d.Bes2 > 0)
            .Join(_context.Sanads.AsNoTracking(), d => new { d.IdSal, Id = d.IdSanad }, s => new { s.IdSal, s.Id },
                (d, s) => new { Detail = d, Sanad = s })
            .Where(x => !x.Sanad.Disable && string.Compare(x.Sanad.SabtDate, fromDate) >= 0 && string.Compare(x.Sanad.SabtDate, toDate) <= 0)
            .Select(x => new
            {
                x.Detail.IdKala,
                Quantity = x.Detail.Bes2,
                SalesAmount = x.Detail.SumMab,
                PurchaseCost = (decimal)x.Detail.Bes2 * x.Detail.BedMabKharid
            })
            .ToListAsync(cancellationToken);

        var items = details.GroupBy(x => x.IdKala).Select(g => new ProfitReportItemResponse
        {
            IdKala = g.Key,
            Quantity = g.Sum(x => x.Quantity),
            SalesAmount = g.Sum(x => x.SalesAmount),
            PurchaseCost = g.Sum(x => x.PurchaseCost),
            Profit = g.Sum(x => x.SalesAmount - x.PurchaseCost)
        }).OrderByDescending(x => x.Profit).ToList();

        return Ok(new ProfitReportResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalSales = items.Sum(x => x.SalesAmount),
            TotalPurchaseCost = items.Sum(x => x.PurchaseCost),
            TotalProfit = items.Sum(x => x.Profit),
            Items = items
        });
    }
}
