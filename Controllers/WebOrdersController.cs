using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web-orders")]
public sealed class WebOrdersController : ControllerBase
{
    private const int PendingSanadType = 7;
    private const int FinalSaleSanadType = 12;
    private readonly KianStoreDbContext _context;
    private readonly IDocumentService _documentService;
    private readonly IStockService _stockService;

    public WebOrdersController(KianStoreDbContext context, IDocumentService documentService, IStockService stockService)
    { _context = context; _documentService = documentService; _stockService = stockService; }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] WebOrderCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResult("ORDER_ITEMS_REQUIRED", "حداقل یک کالا برای ثبت سفارش لازم است."));
        if (!request.TarafId.HasValue || !request.TarafType.HasValue)
            return BadRequest(ApiResponse<object>.ErrorResult("CUSTOMER_REQUIRED", "برای ثبت سفارش وب، طرف حساب الزامی است."));

        var idSal = request.IdSal ?? 1405;
        var idAnbar = request.IdAnbar ?? 1;
        var requestedItems = request.Items.Where(x => !string.IsNullOrWhiteSpace(x.KalaId) && x.Quantity > 0)
            .GroupBy(x => x.KalaId.Trim()).Select(g => new { KalaId = g.Key, Quantity = g.Sum(x => x.Quantity) }).ToList();
        if (requestedItems.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResult("ORDER_ITEMS_INVALID", "اقلام سفارش معتبر نیستند."));

        var kalaIds = requestedItems.Select(x => x.KalaId).ToArray();
        var kalas = await _context.Kalas.AsNoTracking().Where(x => kalaIds.Contains(x.Id) && !x.IsDisabled).ToDictionaryAsync(x => x.Id, cancellationToken);
        var missing = kalaIds.FirstOrDefault(x => !kalas.ContainsKey(x));
        if (missing is not null)
            return NotFound(ApiResponse<object>.ErrorResult("PRODUCT_NOT_FOUND", $"کالا با شناسه {missing} یافت نشد."));

        var tarafExists = await _context.Tarafs.AsNoTracking().AnyAsync(x => x.Id == request.TarafId.Value && x.IdType == request.TarafType.Value && !x.IsDisabled, cancellationToken);
        if (!tarafExists)
            return BadRequest(ApiResponse<object>.ErrorResult("CUSTOMER_NOT_FOUND", "طرف حساب انتخاب‌شده یافت نشد."));

        foreach (var item in requestedItems)
        {
            var stock = await _stockService.CheckAsync(item.KalaId, item.Quantity, idAnbar, idSal, cancellationToken);
            if (!stock.IsAvailable)
                return Conflict(ApiResponse<object>.ErrorResult("INSUFFICIENT_STOCK", $"موجودی کالای {item.KalaId} کافی نیست.", new { stock.Available, stock.Requested, stock.KalaId }));
        }

        var orderNumber = BuildOrderNumber(DateTime.UtcNow);
        var documentRequest = new CreateDocumentRequest
        {
            IdSal = idSal,
            SanadType = PendingSanadType,
            IdAnbar = idAnbar,
            IdTaraf = request.TarafId.Value,
            IdTarafType = request.TarafType.Value,
            IdMasool = 101,
            IdSandogh = 1,
            IdSandoghType = 1,
            SabtDate = DateTime.Now.ToString("yyyy/MM/dd"),
            Des = $"سفارش وبسایت - {orderNumber}",
            Sharh = request.Notes?.Trim(),
            CheckStock = false,
            IsPending = true,
            SefareshID = orderNumber,
            Items = requestedItems.Select(item => new CreateDocumentItemRequest
            {
                IdKala = item.KalaId,
                Quantity = item.Quantity,
                UnitPrice = kalas[item.KalaId].MabFrosh,
                PurchasePrice = null,
                IsIncoming = false
            }).ToList()
        };

        var document = await _documentService.CreateAsync(documentRequest, cancellationToken);
        if (!document.Success || document.Data is null) return StatusCode(500, document);

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            document.Data.IdSal,
            document.Data.Id,
            document.Data.IdFaktor,
            OrderNumber = orderNumber,
            SanadType = PendingSanadType,
            document.Data.TotalAmount
        }, "سفارش مستقیماً به سند در انتظار تأیید ثبت شد."));
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<object>>> GetPending(CancellationToken cancellationToken)
    {
        var sanads = await _context.Sanads.AsNoTracking()
            .Where(x => x.SanadType == PendingSanadType && !x.Disable)
            .OrderBy(x => x.SabtDate).ThenBy(x => x.IdFaktor).Take(100).ToListAsync(cancellationToken);
        if (sanads.Count == 0) return Ok(ApiResponse<object>.SuccessResult(Array.Empty<object>(), "سند در انتظار تأیید وجود ندارد."));

        var ids = sanads.Select(x => x.Id).ToList();
        var details = await _context.SanadDetails.AsNoTracking().Where(x => ids.Contains(x.IdSanad) && x.IdSal == sanads[0].IdSal)
            .OrderBy(x => x.IdSanad).ThenBy(x => x.Id2).ToListAsync(cancellationToken);
        var kalaIds = details.Select(x => x.IdKala).Distinct().ToList();
        var kalas = await _context.Kalas.AsNoTracking().Where(x => kalaIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var tarafIds = sanads.Select(x => x.IdTaraf).Distinct().ToList();
        var tarafs = await _context.Tarafs.AsNoTracking().Where(x => tarafIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var lookup = details.ToLookup(x => x.IdSanad);

        var result = sanads.Select(s => new
        {
            s.IdSal,
            s.Id,
            OrderNumber = s.SefareshID ?? $"S{s.IdFaktor}",
            s.IdFaktor,
            s.SanadType,
            s.IdAnbar,
            s.IdTaraf,
            s.IdTarafType,
            TarafName = tarafs.FirstOrDefault(t => t.Id == s.IdTaraf && t.IdType == s.IdTarafType)?.Name,
            SabtDate = s.SabtDate,
            TotalAmount = s.MabKol,
            Description = s.Des,
            Items = lookup[s.Id].Select(d => new
            {
                Id = d.Id2,
                d.IdKala,
                KalaName = kalas.TryGetValue(d.IdKala, out var k) ? k.KalaName : d.IdKala,
                Quantity = d.Bes2,
                UnitPrice = d.BesMab2,
                TotalPrice = d.SumMab,
                PurchasePrice = d.BedMabKharid
            })
        }).ToList();
        return Ok(ApiResponse<object>.SuccessResult(result, "فاکتورهای وب در انتظار تأیید دریافت شد."));
    }

    [HttpPost("{orderNumber}/finalize")]
    public async Task<ActionResult<ApiResponse<object>>> Finalize(string orderNumber, [FromBody] WebPendingOrderFinalizeRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResult("PURCHASE_PRICES_REQUIRED", "قیمت خرید اقلام الزامی است."));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var sanad = await _context.Sanads.FirstOrDefaultAsync(x => x.SefareshID == orderNumber && x.SanadType == PendingSanadType && !x.Disable, cancellationToken);
        if (sanad is null)
        {
            var alreadyFinal = await _context.Sanads.AsNoTracking().AnyAsync(x => x.SefareshID == orderNumber && x.SanadType == FinalSaleSanadType && !x.Disable, cancellationToken);
            if (alreadyFinal) return Conflict(ApiResponse<object>.ErrorResult("ORDER_ALREADY_FINALIZED", "این سند قبلاً تأیید شده است."));
            return NotFound(ApiResponse<object>.ErrorResult("ORDER_NOT_FOUND", "سند وب در انتظار تأیید یافت نشد."));
        }

        var details = await _context.SanadDetails.Where(x => x.IdSal == sanad.IdSal && x.IdSanad == sanad.Id).OrderBy(x => x.Id2).ToListAsync(cancellationToken);
        var prices = request.Items.Where(x => !string.IsNullOrWhiteSpace(x.KalaId)).GroupBy(x => x.KalaId.Trim()).ToDictionary(x => x.Key, x => x.Last().PurchasePrice);
        var missing = details.Where(x => !prices.TryGetValue(x.IdKala, out var p) || p <= 0).Select(x => x.IdKala).Distinct().ToList();
        if (missing.Count > 0) return BadRequest(ApiResponse<object>.ErrorResult("PURCHASE_PRICES_REQUIRED", "برای همه اقلام قیمت خرید واحد وارد شود.", new { KalaIds = missing }));

        foreach (var detail in details)
        {
            detail.BedMabKharid = prices[detail.IdKala];
            detail.SanadType = FinalSaleSanadType;
        }
        sanad.SanadType = FinalSaleSanadType;
        sanad.IsFinal = true;
        sanad.IsSavedFinal = true;
        if (!string.IsNullOrWhiteSpace(request.SabtDate)) sanad.SabtDate = request.SabtDate;
        if (!string.IsNullOrWhiteSpace(request.Des)) sanad.Des = request.Des;
        if (!string.IsNullOrWhiteSpace(request.Sharh)) sanad.Sharh = request.Sharh;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResult(new { sanad.IdSal, sanad.Id, sanad.IdFaktor, sanad.SanadType, sanad.IsFinal }, "همان سند با موفقیت تأیید و نهایی شد."));
    }

    private static string BuildOrderNumber(DateTime utcNow) => $"W{utcNow:yyyyMMddHHmmssfff}";
}
