using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.Models.KianStore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

/// <summary>
/// Compatibility endpoints used by the mobile app's "فاکتورهای وبسایت" screen.
/// Website orders are stored in the existing Sanad/SanadDetail tables; no new
/// web-order tables are created.
/// </summary>
[ApiController]
[Route("api/web-orders")]
public sealed class WebOrdersLegacyCompatibilityController : ControllerBase
{
    private const int WebsiteSaleSanadType = 12;
    private const string WebsiteDescription = "سفارش ثبت‌شده از وب‌سایت";

    private readonly KianStoreDbContext _context;

    public WebOrdersLegacyCompatibilityController(KianStoreDbContext context)
    {
        _context = context;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<object>>> GetPending(CancellationToken cancellationToken = default)
    {
        var sanads = await _context.Sanads.AsNoTracking()
            .Where(x => x.SanadType == WebsiteSaleSanadType &&
                        !x.Disable &&
                        x.Des != null &&
                        x.Des.Contains(WebsiteDescription))
            .OrderByDescending(x => x.IdFaktor)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (sanads.Count == 0)
            return Ok(ApiResponse<object>.SuccessResult(Array.Empty<object>(), "فاکتور وبسایتی وجود ندارد."));

        var ids = sanads.Select(x => x.Id).ToList();
        var idSal = sanads[0].IdSal;

        var details = await _context.SanadDetails.AsNoTracking()
            .Where(x => x.IdSal == idSal && ids.Contains(x.IdSanad))
            .OrderBy(x => x.IdSanad)
            .ThenBy(x => x.Id2)
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
            orderNumber = $"S{s.IdFaktor}",
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

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResult("PURCHASE_PRICES_REQUIRED", "قیمت خرید اقلام الزامی است."));

        var factorId = ParseFactor(orderNumber);
        if (!factorId.HasValue)
            return BadRequest(ApiResponse<object>.ErrorResult("INVALID_ORDER_NUMBER", "شماره سفارش وب معتبر نیست."));

        var sanad = await _context.Sanads
            .FirstOrDefaultAsync(x => x.IdFaktor == factorId.Value &&
                                      x.SanadType == WebsiteSaleSanadType &&
                                      !x.Disable &&
                                      x.Des != null &&
                                      x.Des.Contains(WebsiteDescription), cancellationToken);

        if (sanad is null)
            return NotFound(ApiResponse<object>.ErrorResult("ORDER_NOT_FOUND", "فاکتور وبسایت پیدا نشد."));

        var details = await _context.SanadDetails
            .Where(x => x.IdSal == sanad.IdSal && x.IdSanad == sanad.Id)
            .OrderBy(x => x.Id2)
            .ToListAsync(cancellationToken);

        var prices = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.KalaId))
            .GroupBy(x => x.KalaId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().PurchasePrice, StringComparer.OrdinalIgnoreCase);

        var missing = details
            .Where(x => !prices.TryGetValue(x.IdKala, out var price) || price <= 0)
            .Select(x => x.IdKala)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count > 0)
            return BadRequest(ApiResponse<object>.ErrorResult(
                "PURCHASE_PRICES_REQUIRED",
                "برای همه اقلام قیمت خرید واحد وارد شود.",
                new { KalaIds = missing }));

        foreach (var detail in details)
            detail.BedMabKharid = prices[detail.IdKala];

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResult(
            new
            {
                sanad.IdSal,
                sanad.Id,
                sanad.IdFaktor,
                sanad.SanadType,
                isFinal = sanad.IsFinal || sanad.IsSavedFinal
            },
            "قیمت خرید اقلام با موفقیت ثبت شد."));
    }

    private static int? ParseFactor(string orderNumber)
    {
        var value = orderNumber.Trim();
        if (value.StartsWith("S", StringComparison.OrdinalIgnoreCase))
            value = value[1..];
        return int.TryParse(value, out var result) && result > 0 ? result : null;
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
