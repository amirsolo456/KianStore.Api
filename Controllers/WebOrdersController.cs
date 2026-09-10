using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Customers;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.DTOs.WebOrders;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

/// <summary>
/// Website checkout facade. It reuses the existing KianStore customer/document/stock
/// services and existing tables; no web-specific business schema is created.
/// </summary>
[ApiController]
[Route("api/web/orders")]
public sealed class WebOrdersController : ControllerBase
{
    private readonly KianStoreDbContext _context;
    private readonly ICustomerService _customerService;
    private readonly IDocumentService _documentService;
    private readonly IStockService _stockService;
    private readonly IConfiguration _configuration;

    public WebOrdersController(
        KianStoreDbContext context,
        ICustomerService customerService,
        IDocumentService documentService,
        IStockService stockService,
        IConfiguration configuration)
    {
        _context = context;
        _customerService = customerService;
        _documentService = documentService;
        _stockService = stockService;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WebOrderCreatedResponse>>> Create(
        [FromBody] CreateWebOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("EMPTY_ORDER", "سبد خرید خالی است."));

        var idSal = GetInt("WebOrder:IdSal", 1405);
        var sanadType = GetInt("WebOrder:SanadType", 12);
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
        {
            return StatusCode(500, ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                "WEB_ORDER_CASHBOX_NOT_CONFIGURED",
                "هیچ صندوق معتبری برای ثبت سفارش پیدا نشد. در KianStore یک صندوق معتبر/انتخاب‌شده تعریف کنید یا WebOrderIdSandogh و WebOrderIdSandoghType را در server.config.txt تنظیم کنید."));
        }

        var mobile = request.Mobile.Trim();
        if (mobile.Length == 0)
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("INVALID_MOBILE", "شماره موبایل الزامی است."));

        // Collapse duplicate product rows and validate stock before creating the document.
        var uniqueItems = request.Items
            .GroupBy(x => x.IdKala.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new CreateWebOrderItem
            {
                IdKala = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        foreach (var item in uniqueItems)
        {
            if (item.Quantity <= 0)
                return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("INVALID_QUANTITY", "تعداد کالا باید بیشتر از صفر باشد."));

            var stock = await _stockService.CheckAsync(item.IdKala, item.Quantity, idAnbar, idSal, cancellationToken);
            if (!stock.IsAvailable)
            {
                return Conflict(ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                    "INSUFFICIENT_STOCK",
                    $"موجودی کالای {item.IdKala} کافی نیست. موجودی قابل فروش: {stock.Available}."));
            }
        }

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
                Phone = request.Phone,
                Address = request.Address
            });

            if (!createCustomer.Success || createCustomer.Data == null)
                return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                    createCustomer.Code ?? "CUSTOMER_CREATE_FAILED",
                    createCustomer.Message ?? "ثبت مشتری انجام نشد."));

            customer = ApiResponse<CustomerResponse>.SuccessResult(createCustomer.Data);
        }

        var document = await _documentService.CreateAsync(new CreateDocumentRequest
        {
            IdSal = idSal,
            SanadType = sanadType,
            IdAnbar = idAnbar,
            IdTaraf = customer.Data!.Id,
            IdTarafType = 2,
            IdMasool = idMasool,
            IdSandogh = cashbox.Id,
            IdSandoghType = cashbox.Type,
            SabtDate = DateTime.Now.ToString("yyyy/MM/dd"),
            Des = "سفارش ثبت‌شده از وب‌سایت",
            Sharh = request.Description,
            CheckStock = true,
            Items = uniqueItems.Select(x => new CreateDocumentItemRequest
            {
                IdKala = x.IdKala,
                Quantity = x.Quantity,
                // Never accept a browser-supplied price. The document service uses
                // the current authoritative KianStore price when this is null.
                UnitPrice = null,
                IsIncoming = false
            }).ToList()
        }, cancellationToken);

        if (!document.Success || document.Data == null)
        {
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                document.Code ?? "ORDER_CREATE_FAILED",
                document.Message ?? "ثبت سفارش انجام نشد."));
        }

        return StatusCode(201, ApiResponse<WebOrderCreatedResponse>.SuccessResult(
            new WebOrderCreatedResponse
            {
                IdSal = document.Data.IdSal,
                Id = document.Data.Id,
                IdFaktor = document.Data.IdFaktor,
                IdTaraf = document.Data.IdTaraf,
                TotalAmount = document.Data.TotalAmount
            },
            "سفارش با موفقیت ثبت شد."));
    }

    private int GetInt(string key, int fallback)
        => int.TryParse(_configuration[key], out var value) ? value : fallback;
}
