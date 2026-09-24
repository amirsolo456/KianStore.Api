using System.Text.Json;
using System.Text.Json.Nodes;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Services.Implementations;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private const int PurchaseType = 11;
    private const int PartnerSaleType = 113;
    private readonly KianStoreDbContext _context;
    private readonly IDocumentService _documentService;
    private readonly IDocumentMutationService _mutationService;
    private readonly ISanadAuditService _auditService;

    public DocumentsController(KianStoreDbContext context, IDocumentService documentService, IDocumentMutationService mutationService, ISanadAuditService auditService)
    {
        _context = context;
        _documentService = documentService;
        _mutationService = mutationService;
        _auditService = auditService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await _documentService.CreateAsync(request, cancellationToken);
        if (!result.Success || result.Data == null) return StatusCode(201, result);
        var persisted = await _documentService.GetAsync(result.Data.IdSal, result.Data.Id, cancellationToken);
        if (!persisted.Success || persisted.Data == null)
            return StatusCode(500, ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_RESPONSE_LOAD_FAILED", "سند ثبت شد اما اطلاعات نهایی آن از پایگاه داده قابل بازیابی نبود."));
        return StatusCode(201, new ApiResponse<DocumentResponse> { Success = true, Code = result.Code, Message = result.Message, Data = persisted.Data, Errors = result.Errors, Warnings = result.Warnings, TraceId = result.TraceId });
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> CreatePurchase([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = DeserializeForced(body, PurchaseType, "اطلاعات سند خرید معتبر نیست.");
        if (request == null) return BadRequest(ApiResponse<DocumentResponse>.ErrorResult("INVALID_REQUEST", "اطلاعات سند خرید معتبر نیست."));
        var result = await _documentService.CreateAsync(request, cancellationToken);
        return await CreatedResponseAsync(result, cancellationToken, "سند خرید ثبت شد اما اطلاعات نهایی آن از پایگاه داده قابل بازیابی نبود.");
    }

    [HttpDelete("purchase/{idSal:int}/{id}")]
    public async Task<IActionResult> DeletePurchase(int idSal, string id, CancellationToken cancellationToken)
    {
        var result = await _mutationService.DeletePurchaseAsync(idSal, id, GetCurrentUserId(null), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{idSal:int}/{id}")]
    public async Task<IActionResult> DeleteLegacy(int idSal, string id, CancellationToken cancellationToken)
    {
        var document = await _documentService.GetAsync(idSal, id, cancellationToken);
        if (!document.Success || document.Data == null)
            return NotFound(ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_NOT_FOUND", "سند مورد نظر یافت نشد."));

        return document.Data.SanadType switch
        {
            PurchaseType => Ok(await _mutationService.DeletePurchaseAsync(idSal, id, GetCurrentUserId(null), cancellationToken)),
            PartnerSaleType => Ok(await _mutationService.DeletePartnerSaleAsync(idSal, id, GetCurrentUserId(null), cancellationToken)),
            _ => BadRequest(ApiResponse<DocumentResponse>.ErrorResult("DELETE_NOT_SUPPORTED", "حذف این نوع سند از این مسیر پشتیبانی نمی‌شود."))
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLegacyById(string id, CancellationToken cancellationToken)
    {
        var sanad = await _context.Sanads.AsNoTracking().Where(x => x.Id == id && !x.Disable && (x.SanadType == PurchaseType || x.SanadType == PartnerSaleType)).OrderByDescending(x => x.IdSal).FirstOrDefaultAsync(cancellationToken);
        if (sanad == null) return NotFound(ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_NOT_FOUND", "سند مورد نظر یافت نشد."));
        return sanad.SanadType switch
        {
            PurchaseType => Ok(await _mutationService.DeletePurchaseAsync(sanad.IdSal, sanad.Id, GetCurrentUserId(null), cancellationToken)),
            PartnerSaleType => Ok(await _mutationService.DeletePartnerSaleAsync(sanad.IdSal, sanad.Id, GetCurrentUserId(null), cancellationToken)),
            _ => BadRequest(ApiResponse<DocumentResponse>.ErrorResult("DELETE_NOT_SUPPORTED", "حذف این نوع سند از این مسیر پشتیبانی نمی‌شود."))
        };
    }

    [HttpPost("partner-sale")]
    public async Task<IActionResult> CreatePartnerSale([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var request = DeserializeForced(body, PartnerSaleType, "اطلاعات فروش از انبار همکار معتبر نیست.");
        if (request == null) return BadRequest(ApiResponse<DocumentResponse>.ErrorResult("INVALID_REQUEST", "اطلاعات فروش از انبار همکار معتبر نیست."));
        request = new CreateDocumentRequest
        {
            IdSal = request.IdSal, SanadType = PartnerSaleType, IdAnbar = request.IdAnbar, IdTaraf = request.IdTaraf, IdTarafType = request.IdTarafType,
            IdMasool = request.IdMasool, IdFaktor = request.IdFaktor, IdSandogh = request.IdSandogh, IdSandoghType = request.IdSandoghType,
            SabtDate = request.SabtDate, Des = request.Des, Sharh = request.Sharh, CheckStock = false, IsPending = false,
            SefareshID = request.SefareshID, DiscountCodes = request.DiscountCodes, NextPurchaseDiscount = request.NextPurchaseDiscount, Items = request.Items
        };
        var result = await _documentService.CreateAsync(request, cancellationToken);
        var response = await CreatedResponseAsync(result, cancellationToken, "فروش ثبت شد اما اطلاعات نهایی آن از پایگاه داده قابل بازیابی نبود.");
        if (response is ObjectResult { Value: ApiResponse<DocumentResponse> api } && api.Success && api.Data != null)
            await _auditService.RecordAsync(api.Data, GetCurrentUserId(request.IdMasool), "CREATE", "ثبت سند فروش از انبار همکار", cancellationToken);
        return response;
    }

    [HttpPut("partner-sale/{idSal:int}/{id}")]
    public async Task<IActionResult> UpdatePartnerSale(int idSal, string id, [FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await _mutationService.UpdatePartnerSaleAsync(idSal, id, request, GetCurrentUserId(request.IdMasool), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("partner-sale/{idSal:int}/{id}")]
    public async Task<IActionResult> DeletePartnerSale(int idSal, string id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mutationService.DeletePartnerSaleAsync(idSal, id, GetCurrentUserId(null), cancellationToken);
            return Ok(result);
        }
        catch (ApiException ex) when (ex.Code == "PARTNER_SALE_NOT_FOUND")
        {
            var result = await _mutationService.DeletePurchaseAsync(idSal, id, GetCurrentUserId(null), cancellationToken);
            return Ok(result);
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int idSal, [FromQuery] int sanadType, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
        => Ok(await _documentService.GetHistoryAsync(idSal, sanadType, page, pageSize, cancellationToken));

    [HttpPut("{idSal:int}/{id}/bookmark")]
    public async Task<IActionResult> SetBookmark(
        int idSal,
        string id,
        [FromBody] DocumentBookmarkRequest request,
        CancellationToken cancellationToken)
    {
        var sanad = await _context.Sanads
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.IdSal == idSal && x.Id == id && !x.Disable,
                cancellationToken);

        if (sanad == null)
            return NotFound(ApiResponse<DocumentBookmarkResponse>.ErrorResult(
                "DOCUMENT_NOT_FOUND",
                "سند مورد نظر یافت نشد یا غیرفعال است."));

        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.Sanad
SET IsBookmarked = {request.IsBookmarked}
WHERE IdSal = {idSal}
  AND Id = {id}
  AND Disable = 0;", cancellationToken);

        if (affected == 0)
            return NotFound(ApiResponse<DocumentBookmarkResponse>.ErrorResult(
                "DOCUMENT_NOT_FOUND",
                "سند مورد نظر یافت نشد یا غیرفعال است."));

        return Ok(ApiResponse<DocumentBookmarkResponse>.SuccessResult(
            new DocumentBookmarkResponse
            {
                IdSal = idSal,
                Id = id,
                IsBookmarked = request.IsBookmarked,
                Message = request.IsBookmarked
                    ? "سند نشان شد."
                    : "نشان سند برداشته شد."
            },
            request.IsBookmarked
                ? "سند با موفقیت نشان شد."
                : "نشان سند با موفقیت برداشته شد."));
    }

    [HttpGet("{idSal:int}/{id}")]
    public async Task<IActionResult> Get(int idSal, string id, CancellationToken cancellationToken)
        => Ok(await _documentService.GetAsync(idSal, id, cancellationToken));

    private CreateDocumentRequest? DeserializeForced(JsonElement body, int sanadType, string message)
    {
        if (body.ValueKind != JsonValueKind.Object) return null;
        var obj = JsonNode.Parse(body.GetRawText())?.AsObject();
        if (obj == null) return null;
        obj["sanadType"] = sanadType;
        return obj.Deserialize<CreateDocumentRequest>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private async Task<IActionResult> CreatedResponseAsync(ApiResponse<DocumentResponse> result, CancellationToken ct, string loadError)
    {
        if (!result.Success || result.Data == null) return StatusCode(201, result);
        var persisted = await _documentService.GetAsync(result.Data.IdSal, result.Data.Id, ct);
        if (!persisted.Success || persisted.Data == null) return StatusCode(500, ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_RESPONSE_LOAD_FAILED", loadError));
        return StatusCode(201, new ApiResponse<DocumentResponse> { Success = true, Code = result.Code, Message = result.Message, Data = persisted.Data, Errors = result.Errors, Warnings = result.Warnings, TraceId = result.TraceId });
    }

    private int? GetCurrentUserId(int? fallback)
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var raw) && int.TryParse(raw.FirstOrDefault(), out var userId) && userId > 0) return userId;
        return fallback;
    }
}
