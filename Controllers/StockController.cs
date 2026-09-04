using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/stock")]
public sealed class StockController : ControllerBase
{
    private readonly IStockService _stockService;
    private readonly KianStoreDbContext _context;

    public StockController(IStockService stockService, KianStoreDbContext context)
    {
        _stockService = stockService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int idAnbar = 1,
        [FromQuery] int idSal = 1405,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = from kd in _context.KalaDetails.AsNoTracking()
                    join k in _context.Kalas.AsNoTracking() on kd.IdKala equals k.Id
                    where !k.IsDisabled && kd.IdAnbar == idAnbar
                    select new { kd, k };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(x => x.k.Id.Contains(value) || x.k.KalaName.Contains(value) || x.k.Barcode.Contains(value));
        }

        var items = await query
            .OrderBy(x => x.k.KalaName)
            .ThenBy(x => x.k.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                kalaId = x.k.Id,
                name = x.k.KalaName,
                barcode = x.k.Barcode,
                idAnbar,
                idSal,
                stock = (decimal)x.kd.Quantity,
                lastPurchasePrice = x.kd.LastMabKharid,
                salePrice = x.kd.MabFrosh ?? x.k.MabFrosh,
                salePrice1 = x.kd.MabFrosh1
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResult(
            new { page, pageSize, items },
            "لیست موجودی با موفقیت دریافت شد."));
    }

    [HttpGet("{kalaId}")]
    public async Task<IActionResult> GetStock(
        string kalaId,
        [FromQuery] int idAnbar = 1,
        [FromQuery] int idSal = 1405,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kalaId))
            throw new BusinessException("INVALID_PRODUCT_ID", "شناسه کالا معتبر نیست.");

        var stock = await _stockService.GetStockAsync(kalaId, idAnbar, idSal, cancellationToken);
        return Ok(ApiResponse<object>.SuccessResult(
            new { kalaId, idAnbar, idSal, stock },
            "موجودی کالا با موفقیت دریافت شد."));
    }

    [HttpGet("{kalaId}/check")]
    public async Task<IActionResult> CheckStock(
        string kalaId,
        [FromQuery] decimal quantity,
        [FromQuery] int idAnbar = 1,
        [FromQuery] int idSal = 1405,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kalaId))
            throw new BusinessException("INVALID_PRODUCT_ID", "شناسه کالا معتبر نیست.");
        if (quantity <= 0)
            throw new BusinessException("INVALID_QUANTITY", "تعداد باید بیشتر از صفر باشد.");

        var result = await _stockService.CheckAsync(kalaId, quantity, idAnbar, idSal, cancellationToken);
        if (!result.IsAvailable)
        {
            return Conflict(ApiResponse<StockCheckResult>.ErrorResult(
                "INSUFFICIENT_STOCK",
                $"موجودی کالای {kalaId} کافی نیست. موجودی: {result.Available}، درخواست: {result.Requested}.",
                result));
        }

        return Ok(ApiResponse<StockCheckResult>.SuccessResult(result, "موجودی برای تعداد درخواستی کافی است."));
    }
}
