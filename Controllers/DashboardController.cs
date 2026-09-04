using KianStore.Api.Common;
using KianStore.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly KianStoreDbContext _context;

    public DashboardController(KianStoreDbContext context)
    {
        _context = context;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] int idSal = 1405,
        [FromQuery] int idAnbar = 1,
        CancellationToken ct = default)
    {
        var activeProducts = await _context.Kalas.AsNoTracking().CountAsync(x => !x.IsDisabled, ct);
        var customers = await _context.Tarafs.AsNoTracking().CountAsync(x => !x.IsDisabled && x.IdType == 2, ct);
        var warehouses = await _context.Anbars.AsNoTracking().CountAsync(x => !x.NoActive, ct);
        var activePublicDiscountCodes = await _context.DiscountCodes.AsNoTracking()
            .CountAsync(x => x.IsActive && x.Scope == 1, ct);
        var failedSms = await _context.SmsLogs.AsNoTracking()
            .CountAsync(x => x.Status == 3, ct);
        var pendingSms = await _context.SmsLogs.AsNoTracking()
            .CountAsync(x => x.Status == 1, ct);

        var documentCount = await _context.Sanads.AsNoTracking()
            .CountAsync(x => x.IdSal == idSal && !x.Disable, ct);

        var stockRows = await _context.KalaDetails.AsNoTracking()
            .CountAsync(x => x.IdAnbar == idAnbar, ct);

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            idSal,
            idAnbar,
            activeProducts,
            customers,
            warehouses,
            documentCount,
            stockRows,
            activePublicDiscountCodes,
            pendingSms,
            failedSms
        }, "خلاصه وضعیت با موفقیت دریافت شد."));
    }
}
