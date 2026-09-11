using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web-orders")]
public sealed class WebPendingOrdersController : ControllerBase
{
    private readonly KianStoreDbContext _context;
    private readonly IDocumentService _documentService;

    public WebPendingOrdersController(KianStoreDbContext context, IDocumentService documentService)
    {
        _context = context;
        _documentService = documentService;
    }

    // فقط سفارش‌های وب که هنوز توسط پرسنل نهایی نشده‌اند.
    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<object>>> GetPending(CancellationToken cancellationToken)
    {
        var orders = await _context.Set<MobileOrder>()
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.Status == 1 && x.SanadId == null)
            .OrderBy(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var kalaIds = orders.SelectMany(x => x.Items).Select(x => x.KalaId).Distinct().ToList();
        var kalas = await _context.Kalas.AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var result = orders.Select(order => new
        {
            order.Id,
            order.OrderNumber,
            order.FirstName,
            order.LastName,
            order.Mobile,
            order.Address,
            order.Status,
            order.TarafId,
            order.TarafType,
            order.CreatedAt,
            order.Notes,
            TotalAmount = order.Items.Sum(x => x.TotalPrice),
            Items = order.Items.OrderBy(x => x.Id).Select(item => new
            {
                item.Id,
                item.KalaId,
                KalaName = kalas.TryGetValue(item.KalaId, out var kala) ? kala.KalaName : item.KalaId,
                item.Quantity,
                item.UnitPrice,
                item.TotalPrice
            })
        }).ToList();

        return Ok(ApiResponse<object>.SuccessResult(result, "سفارش‌های در انتظار دریافت شد."));
    }

    // این endpoint تنها نقطه‌ای است که سفارش وب را به Sanad/SanadDetail تبدیل می‌کند.
    [HttpPost("{orderNumber}/finalize")]
    public async Task<ActionResult<ApiResponse<object>>> Finalize(
        string orderNumber,
        [FromBody] WebPendingOrderFinalizeRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _context.Set<MobileOrder>()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
            return NotFound(ApiResponse<object>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد."));

        if (order.SanadId is not null)
            return Conflict(ApiResponse<object>.ErrorResult("ORDER_ALREADY_FINALIZED", "این سفارش قبلاً به سند تبدیل شده است."));

        if (order.Status != 1)
            return Conflict(ApiResponse<object>.ErrorResult("ORDER_NOT_PENDING", "این سفارش در وضعیت قابل نهایی‌سازی نیست."));

        if (order.TarafId is null || order.TarafType is null)
            return BadRequest(ApiResponse<object>.ErrorResult(
                "CUSTOMER_REQUIRED",
                "این سفارش هنوز به طرف حساب متصل نشده و امکان صدور سند ندارد."));

        var prices = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.KalaId))
            .GroupBy(x => x.KalaId.Trim())
            .ToDictionary(x => x.Key, x => x.Last().PurchasePrice);

        var missingPrices = order.Items
            .Select(x => x.KalaId)
            .Distinct()
            .Where(id => !prices.ContainsKey(id))
            .ToList();

        if (missingPrices.Count > 0)
            return BadRequest(ApiResponse<object>.ErrorResult(
                "PURCHASE_PRICES_REQUIRED",
                "برای همه اقلام سفارش باید قیمت خرید ثبت شود.",
                new { KalaIds = missingPrices }));

        var documentRequest = new CreateDocumentRequest
        {
            IdSal = request.IdSal,
            SanadType = request.SanadType,
            IdAnbar = request.IdAnbar,
            IdTaraf = order.TarafId.Value,
            IdTarafType = order.TarafType.Value,
            IdMasool = request.IdMasool,
            IdSandogh = request.IdSandogh,
            IdSandoghType = request.IdSandoghType,
            SabtDate = request.SabtDate,
            Des = request.Des ?? $"فروش وبسایت - {order.OrderNumber}",
            Sharh = request.Sharh ?? order.Notes,
            CheckStock = request.CheckStock,
            Items = order.Items.Select(item => new CreateDocumentItemRequest
            {
                IdKala = item.KalaId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                PurchasePrice = prices[item.KalaId],
                IsIncoming = false
            }).ToList()
        };

        var document = await _documentService.CreateAsync(documentRequest, cancellationToken);
        if (!document.Success || document.Data is null)
            return StatusCode(500, document);

        order.SanadId = document.Data.Id;
        order.SanadSal = document.Data.IdSal;
        order.Status = 2;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            order.OrderNumber,
            order.Status,
            order.SanadId,
            order.SanadSal,
            SanadType = document.Data.SanadType,
            FactorNumber = document.Data.IdFaktor
        }, "سفارش با موفقیت توسط پرسنل نهایی و به سند تبدیل شد."));
    }
}
