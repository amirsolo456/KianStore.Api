using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Customers;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.DTOs.WebOrders;
using KianStore.Api.Services.Interfaces;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web/orders")]
[Route("api/web-orders")]
public sealed class WebOrdersControllerV2 : ControllerBase
{
    private const int PendingSanadType = 7;
    private const int FinalSaleSanadType = 12;

    private readonly KianStoreDbContext _context;
    private readonly ICustomerService _customerService;
    private readonly IDocumentService _documentService;
    private readonly IConfiguration _configuration;

    public WebOrdersControllerV2(
        KianStoreDbContext context,
        ICustomerService customerService,
        IDocumentService documentService,
        IConfiguration configuration)
    {
        _context = context;
        _customerService = customerService;
        _documentService = documentService;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WebOrderCreatedResponse>>> Create(
        [FromBody] CreateWebOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!WebOrderOtpStore.IsVerified(request.Mobile, request.VerificationChallenge))
            return Unauthorized(ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                "MOBILE_NOT_VERIFIED", "شماره موبایل تأیید نشده است. ابتدا کد تأیید پیامکی را وارد کنید."));

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("EMPTY_ORDER", "سبد خرید خالی است."));

        var idSal = GetInt("WebOrder:IdSal", 1405);
        var idAnbar = GetInt("WebOrder:IdAnbar", 1);
        var idMasool = GetInt("WebOrder:IdMasool", 101);
        var configuredCashboxId = GetInt("WebOrder:IdSandogh", 0);
        var configuredCashboxType = GetInt("WebOrder:IdSandoghType", 0);

        var cashbox = configuredCashboxId > 0
            ? await _context.CheckDefs.AsNoTracking()
                .Where(x => x.Id == configuredCashboxId && x.Type == configuredCashboxType)
                .Select(x => new { x.Id, x.Type })
                .FirstOrDefaultAsync(cancellationToken)
            : await _context.CheckDefs.AsNoTracking()
                .Where(x => x.IsSelect)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Type })
                .FirstOrDefaultAsync(cancellationToken)
                ?? await _context.CheckDefs.AsNoTracking()
                    .OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.Type })
                    .FirstOrDefaultAsync(cancellationToken);

        if (cashbox is null)
            return StatusCode(500, ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                "WEB_ORDER_CASHBOX_NOT_CONFIGURED",
                "هیچ صندوق معتبری برای ثبت سفارش پیدا نشد. در KianStore یک صندوق معتبر/انتخاب‌شده تعریف کنید یا WebOrderIdSandogh و WebOrderIdSandoghType را در server.config.txt تنظیم کنید."));

        var mobile = request.Mobile.Trim();
        if (mobile.Length == 0)
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("INVALID_MOBILE", "شماره موبایل الزامی است."));

        var uniqueItems = request.Items
            .GroupBy(x => x.IdKala.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new CreateWebOrderItem { IdKala = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        foreach (var item in uniqueItems)
        {
            if (item.Quantity <= 0)
                return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("INVALID_QUANTITY", "تعداد کالا باید بیشتر از صفر باشد."));
        }

        // Website orders deliberately skip stock validation. They are created as
        // pending SanadType=7 so the existing mobile "فاکتورهای وبسایت" view can show them.
        var kalaIds = uniqueItems.Select(x => x.IdKala).ToArray();
        var kalas = await _context.Kalas.AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id) && !x.IsDisabled)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var missingProduct = uniqueItems.FirstOrDefault(x => !kalas.ContainsKey(x.IdKala));
        if (missingProduct is not null)
            return NotFound(ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                "PRODUCT_NOT_FOUND", $"کالا با شناسه {missingProduct.IdKala} یافت نشد."));

        var customer = await _customerService.GetByMobileAsync(mobile);
        if (!customer.Success || customer.Data == null)
        {
            var nameParts = request.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var createCustomer = await _customerService.CreateCustomerAsync(new CreateCustomerRequest
            {
                PersonType = 1,
                FirstName = nameParts.ElementAtOrDefault(0) ?? request.Name.Trim(),
                LastName = nameParts.ElementAtOrDefault(1) ?? string.Empty,
                Mobile = mobile,
                Address = null
            });

            if (!createCustomer.Success || createCustomer.Data == null)
                return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                    createCustomer.Code ?? "CUSTOMER_CREATE_FAILED",
                    createCustomer.Message ?? "ثبت مشتری انجام نشد."));

            customer = ApiResponse<CustomerResponse>.SuccessResult(createCustomer.Data);
        }

        var orderNumber = BuildOrderNumber(DateTime.UtcNow);
        var document = await _documentService.CreateAsync(new CreateDocumentRequest
        {
            IdSal = idSal,
            SanadType = PendingSanadType,
            IdAnbar = idAnbar,
            IdTaraf = customer.Data!.Id,
            IdTarafType = 2,
            IdMasool = idMasool,
            IdSandogh = cashbox.Id,
            IdSandoghType = cashbox.Type,
            SabtDate = DateTime.Now.ToString("yyyy/MM/dd"),
            Des = $"سفارش وبسایت - {orderNumber}",
            Sharh = request.Description,
            CheckStock = false,
            IsPending = true,
            SefareshID = orderNumber,
            Items = uniqueItems.Select(x => new CreateDocumentItemRequest
            {
                IdKala = x.IdKala,
                Quantity = x.Quantity,
                UnitPrice = null,
                PurchasePrice = null,
                IsIncoming = false
            }).ToList()
        }, cancellationToken);

        if (!document.Success || document.Data == null)
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                document.Code ?? "ORDER_CREATE_FAILED", document.Message ?? "ثبت سفارش انجام نشد."));

        return StatusCode(201, ApiResponse<WebOrderCreatedResponse>.SuccessResult(
            new WebOrderCreatedResponse
            {
                IdSal = document.Data.IdSal,
                Id = document.Data.Id,
                IdFaktor = document.Data.IdFaktor,
                IdTaraf = document.Data.IdTaraf,
                TotalAmount = document.Data.TotalAmount
            },
            "سفارش در سند در انتظار تأیید ثبت شد."));
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<object>>> GetPending(CancellationToken cancellationToken = default)
    {
        var sanads = await _context.Sanads.AsNoTracking()
            .Where(x => x.SanadType == PendingSanadType && !x.Disable)
            .OrderBy(x => x.SabtDate)
            .ThenBy(x => x.IdFaktor)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (sanads.Count == 0)
            return Ok(ApiResponse<object>.SuccessResult(Array.Empty<object>(), "سند در انتظار تأیید وجود ندارد."));

        var ids = sanads.Select(x => x.Id).ToList();
        var details = await _context.SanadDetails.AsNoTracking()
            .Where(x => ids.Contains(x.IdSanad))
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
    public async Task<ActionResult<ApiResponse<object>>> Finalize(
        string orderNumber,
        [FromBody] WebPendingOrderFinalizeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ApiResponse<object>.ErrorResult("PURCHASE_PRICES_REQUIRED", "قیمت خرید اقلام الزامی است."));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var sanad = await _context.Sanads.FirstOrDefaultAsync(
            x => x.SefareshID == orderNumber && x.SanadType == PendingSanadType && !x.Disable,
            cancellationToken);

        if (sanad is null)
        {
            var alreadyFinal = await _context.Sanads.AsNoTracking().AnyAsync(
                x => x.SefareshID == orderNumber && x.SanadType == FinalSaleSanadType && !x.Disable,
                cancellationToken);

            if (alreadyFinal)
                return Conflict(ApiResponse<object>.ErrorResult("ORDER_ALREADY_FINALIZED", "این سند قبلاً تأیید شده است."));

            return NotFound(ApiResponse<object>.ErrorResult("ORDER_NOT_FOUND", "سند وب در انتظار تأیید یافت نشد."));
        }

        var details = await _context.SanadDetails
            .Where(x => x.IdSal == sanad.IdSal && x.IdSanad == sanad.Id)
            .OrderBy(x => x.Id2)
            .ToListAsync(cancellationToken);

        var prices = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.KalaId))
            .GroupBy(x => x.KalaId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().PurchasePrice, StringComparer.OrdinalIgnoreCase);

        var missingPrices = details
            .Where(x => !prices.TryGetValue(x.IdKala, out var p) || p <= 0)
            .Select(x => x.IdKala)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingPrices.Count > 0)
            return BadRequest(ApiResponse<object>.ErrorResult(
                "PURCHASE_PRICES_REQUIRED", "برای همه اقلام قیمت خرید واحد وارد شود.", new { KalaIds = missingPrices }));

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

        return Ok(ApiResponse<object>.SuccessResult(
            new { sanad.IdSal, sanad.Id, sanad.IdFaktor, sanad.SanadType, sanad.IsFinal },
            "همان سند با موفقیت تأیید و نهایی شد."));
    }

    private int GetInt(string key, int fallback)
        => int.TryParse(_configuration[key], out var value) ? value : fallback;

    private static string BuildOrderNumber(DateTime utcNow)
        => $"W{utcNow:yyyyMMddHHmmssfff}";
}
