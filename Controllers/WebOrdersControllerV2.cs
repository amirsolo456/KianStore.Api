using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Customers;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.DTOs.WebOrders;
using KianStore.Api.Services.Interfaces;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web/orders")]
public sealed class WebOrdersControllerV2 : ControllerBase
{
    private const int PendingSanadType = 7;

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

        // سفارش وب ابتدا به‌عنوان سند «در انتظار تأیید» ثبت می‌شود.
        // موجودی در وب‌سایت بررسی نمی‌شود؛ بررسی موجودی در مرحله تأیید داخل اپ موبایل انجام می‌شود.
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
            "سفارش با موفقیت ثبت شد و در انتظار تأیید پرسنل قرار گرفت."));
    }

    private static string BuildOrderNumber(DateTime utcNow)
        => $"W{utcNow:yyyyMMddHHmmssfff}";

    private int GetInt(string key, int fallback)
        => int.TryParse(_configuration[key], out var value) ? value : fallback;
}
