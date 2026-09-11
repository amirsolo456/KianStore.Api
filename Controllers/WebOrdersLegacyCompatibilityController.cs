using KianStore.Api.Common;
using KianStore.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

/// <summary>
/// Compatibility endpoints used by the mobile app's website-invoices screen.
/// Website orders are stored in existing Sanad/SanadDetail tables as pending
/// sales (SanadType 7) and become normal sales (SanadType 12) only after staff approval.
/// </summary>
[ApiController]
[Route("api/web-orders")]
public sealed class WebOrdersLegacyCompatibilityController : ControllerBase
{
    private const int PendingWebsiteSanadType = 7;
    private const int FinalSaleSanadType = 12;

    private readonly KianStoreDbContext _context;

    public WebOrdersLegacyCompatibilityController(KianStoreDbContext context)
    {
        _context = context;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<object>>> GetPending(CancellationToken cancellationToken = default)
    {
        var sanads = await _context.Sanads.AsNoTracking()
            .Where(x => x.SanadType == PendingWebsiteSanadType && !x.Disable && !x.IsFinal)
            .OrderBy(x => x.SabtDate)
            .ThenBy(x => x.IdFaktor)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (sanads.Count == 0)
            return Ok(ApiResponse<object>.SuccessResult(Array.Empty<object>(), "فاکتور وبسایتی در انتظار تأیید وجود ندارد."));

        var ids = sanads.Select(x => x.Id).ToList();
        var idSal = sanads[0].IdSal;
        var details = await _context.SanadDetails.AsNoTracking()
            .Where(x => x.IdSal == idSal && ids.Contains(x.IdSanad))
            .OrderBy(x => x.IdSanad).ThenBy(x => x.Id2)
            .ToListAsync(cancellationToken);

        var kalaIds = details.Select(x => x.IdKala).Distinct().ToList();
        var kalas = await _context.Kalas.AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var tarafIds = sanads.Select(x => x.IdTaraf).Distinct().ToList();
        var tarafs = await _context.Tarafs.AsNoTracking()
            .Where(x => tarafIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var lookup = details.ToLookup(x => x.IdSanad);

        var result = sanads.Select(s => new
        {
            id = s.IdFaktor,
            idSal = s.IdSal,
            idSanad = s.Id,
            orderNumber = s.SefareshID ?? $"S{s.IdFaktor}",
            idFaktor = s.IdFaktor,
            sanadType = s.SanadType,
            idAnbar = s.IdAnbar,
            idTaraf = s.IdTaraf,
            idTarafType = s.IdTarafType,
            tarafName = tarafs.FirstOrDefault(t => t.Id == s.IdTaraf && t.IdType == s.IdTarafType)?.Name,
            sabtDate = s.SabtDate,
            totalAmount = s.MabKol,
            description = s.Des,
            items = lookup[s.Id].Select(d => new
            {
                id = d.Id2,
                kalaId = d.IdKala,
                kalaName = kalas.TryGetValue(d.IdKala, out var k) ? k.KalaName : d.IdKala,
                quantity = d.Bes2,
                unitPrice = d.BesMab2,
                totalPrice = d.SumMab,
                purchasePrice = d.BedMabKharid
            }).ToList()
        }).ToList();

        return Ok(ApiResponse<object>.SuccessResult(result, "فاکتورهای وبسایت با موفقیت دریافت شد."));
    }

    [HttpPost("{orderNumber}/finalize")]
    public async Task<ActionResult<ApiResponse<object>>> Finalize(
        string orderNumber,
        [FromBody] WebOrderFinalizeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            return BadRequest(ApiResponse<object>.ErrorResult("INVALID_ORDER_NUMBER", "شماره سفارش معتبر نیست."));
        if (request.Items.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResult("PURCHASE_PRICES_REQUIRED", "قیمت خرید اقلام الزامی است."));

        var pending = await _context.Sanads.FirstOrDefaultAsync(
            x => x.SefareshID == orderNumber && x.SanadType == PendingWebsiteSanadType && !x.Disable && !x.IsFinal,
            cancellationToken);
        if (pending is null)
            return NotFound(ApiResponse<object>.ErrorResult("ORDER_NOT_FOUND", "فاکتور وب در انتظار تأیید پیدا نشد."));

        var details = await _context.SanadDetails
            .Where(x => x.IdSal == pending.IdSal && x.IdSanad == pending.Id)
            .OrderBy(x => x.Id2)
            .ToListAsync(cancellationToken);

        var prices = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.KalaId))
            .GroupBy(x => x.KalaId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().PurchasePrice, StringComparer.OrdinalIgnoreCase);

        var missing = details.Where(x => !prices.TryGetValue(x.IdKala, out var price) || price <= 0)
            .Select(x => x.IdKala).Distinct().ToList();
        if (missing.Count > 0)
            return BadRequest(ApiResponse<object>.ErrorResult("PURCHASE_PRICES_REQUIRED", "برای همه اقلام قیمت خرید واحد وارد شود.", new { KalaIds = missing }));

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var detail in details)
        {
            detail.BedMabKharid = prices[detail.IdKala];
            detail.SanadType = FinalSaleSanadType;
        }

        pending.SanadType = FinalSaleSanadType;
        pending.IsFinal = true;
        pending.IsSavedFinal = true;
        if (!string.IsNullOrWhiteSpace(request.SabtDate)) pending.SabtDate = request.SabtDate!;
        if (!string.IsNullOrWhiteSpace(request.Des)) pending.Des = request.Des;
        if (!string.IsNullOrWhiteSpace(request.Sharh)) pending.Sharh = request.Sharh;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            pending.IdSal,
            pending.Id,
            pending.IdFaktor,
            pending.SanadType,
            isFinal = pending.IsFinal
        }, "سفارش وب با موفقیت تأیید و نهایی شد."));
    }
}

public sealed class WebOrderFinalizeRequest
{
    public List<WebOrderFinalizeItem> Items { get; init; } = new();
}

public sealed class WebOrderFinalizeItem
{
    public string KalaId { get; init; } = string.Empty;
    public decimal PurchasePrice { get; init; }
}
