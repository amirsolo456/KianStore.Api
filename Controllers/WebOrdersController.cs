using KianStore.Api.Common;
using KianStore.Api.DTOs.Customers;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.DTOs.WebOrders;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

/// <summary>
/// Website checkout facade. It reuses the existing customer/document/stock services
/// and therefore writes only to the existing KianStore tables.
/// </summary>
[ApiController]
[Route("api/web/orders")]
public sealed class WebOrdersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly IDocumentService _documentService;
    private readonly IStockService _stockService;
    private readonly IConfiguration _configuration;

    public WebOrdersController(
        ICustomerService customerService,
        IDocumentService documentService,
        IStockService stockService,
        IConfiguration configuration)
    {
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
        var idSandogh = GetInt("WebOrder:IdSandogh", 0);
        var idSandoghType = GetInt("WebOrder:IdSandoghType", 0);

        if (sanadType <= 0 || idSandogh <= 0)
        {
            return StatusCode(500, ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                "WEB_ORDER_CONFIGURATION_MISSING",
                "تنظیمات ثبت سفارش وب‌سایت کامل نیست. WebOrderIdSandogh و WebOrderSanadType را در server.config.txt تنظیم کنید."));
        }

        var mobile = request.Mobile.Trim();
        if (mobile.Length == 0)
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("INVALID_MOBILE", "شماره موبایل الزامی است."));

        // Never trust price values sent by the browser. The existing document service
        // uses KianStore's current MabFrosh when UnitPrice is null.
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
            var firstName = nameParts.ElementAtOrDefault(0) ?? request.Name.Trim();
            var lastName = nameParts.ElementAtOrDefault(1) ?? string.Empty;

            var createCustomer = await _customerService.CreateCustomerAsync(new CreateCustomerRequest
            {
                PersonType = 1,
                FirstName = firstName,
                LastName = lastName,
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
            IdSandogh = idSandogh,
            IdSandoghType = idSandoghType,
            SabtDate = DateTime.Now.ToString("yyyy/MM/dd"),
            Des = "سفارش ثبت‌شده از وب‌سایت",
            Sharh = request.Description,
            CheckStock = true,
            Items = uniqueItems.Select(x => new CreateDocumentItemRequest
            {
                IdKala = x.IdKala,
                Quantity = x.Quantity,
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
