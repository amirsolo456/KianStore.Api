using KianStore.Api.Common;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _documentService.CreateAsync(request, cancellationToken);

        if (!result.Success || result.Data == null)
            return BadRequest(result);

        var persisted = await _documentService.GetAsync(result.Data.IdSal, result.Data.Id, cancellationToken);

        if (!persisted.Success || persisted.Data == null)
        {
            return StatusCode(500, ApiResponse<DocumentResponse>.ErrorResult(
                "DOCUMENT_RESPONSE_LOAD_FAILED",
                "سند ثبت شد اما اطلاعات نهایی آن از پایگاه داده قابل بازیابی نبود."));
        }

        return StatusCode(201, new ApiResponse<DocumentResponse>
        {
            Success = true,
            Code = result.Code,
            Message = result.Message,
            Data = persisted.Data,
            Errors = result.Errors,
            Warnings = result.Warnings,
            TraceId = result.TraceId
        });
    }

    // ثبت سند خرید با همان قرارداد سند فعلی؛ تمام اقلام به‌صورت ورودی ثبت می‌شوند.
    // sanadType عمداً از سمت کلاینت/تنظیمات کسب‌وکار تعیین می‌شود چون نوع سند در DB قدیمی پروژه ممکن است متفاوت باشد.
    [HttpPost("purchase")]
    public async Task<IActionResult> CreatePurchase(
        [FromBody] CreateDocumentRequest request,
        [FromQuery] int sanadType,
        CancellationToken cancellationToken)
    {
        if (sanadType <= 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "INVALID_PURCHASE_DOCUMENT_TYPE",
                "نوع سند خرید معتبر نیست."));
        }

        if (request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "EMPTY_DOCUMENT",
                "سند خرید حداقل باید یک قلم داشته باشد."));
        }

        var purchaseRequest = new CreateDocumentRequest
        {
            IdSal = request.IdSal,
            SanadType = sanadType,
            IdAnbar = request.IdAnbar,
            IdTaraf = request.IdTaraf,
            IdTarafType = request.IdTarafType,
            IdMasool = request.IdMasool,
            IdFaktor = request.IdFaktor,
            IdSandogh = request.IdSandogh,
            IdSandoghType = request.IdSandoghType,
            SabtDate = request.SabtDate,
            Des = request.Des,
            Sharh = request.Sharh,
            CheckStock = false,
            DiscountCodes = request.DiscountCodes,
            NextPurchaseDiscount = null,
            Items = request.Items.Select(x => new CreateDocumentItemRequest
            {
                IdKala = x.IdKala,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                IsIncoming = true,
                Description = x.Description
            }).ToList()
        };

        var result = await _documentService.CreateAsync(purchaseRequest, cancellationToken);
        if (!result.Success || result.Data == null)
            return BadRequest(result);

        var persisted = await _documentService.GetAsync(result.Data.IdSal, result.Data.Id, cancellationToken);
        if (!persisted.Success || persisted.Data == null)
        {
            return StatusCode(500, ApiResponse<DocumentResponse>.ErrorResult(
                "PURCHASE_RESPONSE_LOAD_FAILED",
                "سند خرید ثبت شد اما اطلاعات نهایی آن قابل بازیابی نبود."));
        }

        return StatusCode(201, new ApiResponse<DocumentResponse>
        {
            Success = true,
            Code = result.Code,
            Message = "سند خرید با موفقیت ثبت شد و موجودی کالا افزایش یافت.",
            Data = persisted.Data,
            Errors = result.Errors,
            Warnings = result.Warnings,
            TraceId = result.TraceId
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] int idSal,
        [FromQuery] int sanadType = 12,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _documentService.GetHistoryAsync(idSal, sanadType, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{idSal:int}/{id}")]
    public async Task<IActionResult> Get(
        int idSal,
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _documentService.GetAsync(idSal, id, cancellationToken);
        return Ok(result);
    }
}
