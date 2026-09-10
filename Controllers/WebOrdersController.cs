using KianStore.Api.Common;
using KianStore.Api.DTOs.Customers;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.DTOs.WebOrders;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

/// <summary>
/// Website checkout facade. It reuses the existing customer/document services
/// and therefore writes only to the existing KianStore tables.
/// </summary>
[ApiController]
[Route("api/web/orders")]
public sealed class WebOrdersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly IDocumentService _documentService;
    private readonly IConfiguration _configuration;

    public WebOrdersController(
        ICustomerService customerService,
        IDocumentService documentService,
        IConfiguration configuration)
    {
        _customerService = customerService;
        _documentService = documentService;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WebOrderCreatedResponse>>> Create(
        [FromBody] CreateWebOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("EMPTY_ORDER", "سبد خرید خالی است."));

        var idSal = GetInt("WebOrderIdSal", 1405);
        var sanadType = GetInt("WebOrderSanadType", 12);
        var idAnbar = GetInt("WebOrderIdAnbar", 1);
        var idMasool = GetInt("WebOrderIdMasool", 101);
        var idSandogh = GetInt("WebOrderIdSandogh", 0);
        var idSandoghType = GetInt("WebOrderIdSandoghType", 0);

        if (sanadType <= 0 || idSandogh <= 0)
        {
            return StatusCode(500, ApiResponse<WebOrderCreatedResponse>.ErrorResult(
                "WEB_ORDER_CONFIGURATION_MISSING",
                "تنظیمات ثبت سفارش وب‌سایت کامل نیست. WebOrderSanadType و WebOrderIdSandogh را در server.config.txt تنظیم کنید."));
        }

        var mobile = request.Mobile.Trim();
        if (mobile.Length == 0)
            return BadRequest(ApiResponse<WebOrderCreatedResponse>.ErrorResult("INVALID_MOBILE", "شماره موبایل الزامی است."));

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

            customer = ApiResponse<KianStore.Api.DTOs.Customers.CustomerResponse>.SuccessResult(createCustomer.Data);
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
            Items = request.Items.Select(x => new CreateDocumentItemRequest
            {
                IdKala = x.IdKala,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
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
