using System.Globalization;
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
    private readonly SmsService _smsService;

    public DocumentsController(
        KianStoreDbContext context,
        IDocumentService documentService,
        IDocumentMutationService mutationService,
        ISanadAuditService auditService,
        SmsService smsService)
    {
        _context = context;
        _documentService = documentService;
        _mutationService = mutationService;
        _auditService = auditService;
        _smsService = smsService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await _documentService.CreateAsync(request, cancellationToken);
        if (!result.Success || result.Data == null) return StatusCode(201, result);
        var persisted = await _documentService.GetAsync(result.Data.IdSal, result.Data.Id, cancellationToken);
        if (!persisted.Success || persisted.Data == null)
            return StatusCode(500, ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_RESPONSE_LOAD_FAILED", "سند ثبت شد اما اطلاعات نهایی آن از پایگاه داده قابل بازیابی نبود."));

        await TrySendBuyerDocumentRegistrationSmsAsync(persisted.Data, cancellationToken);

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
        var sanad = await _context.Sanads
            .AsNoTracking()
            .Where(x => x.Id == id && !x.Disable && (x.SanadType == PurchaseType || x.SanadType == PartnerSaleType))
            .OrderByDescending(x => x.IdSal)
            .FirstOrDefaultAsync(cancellationToken);

        if (sanad == null)
            return NotFound(ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_NOT_FOUND", "سند مورد نظر یافت نشد."));

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

    // Single history endpoint for every document type.
    // Example: GET /api/documents/history?idSal=1405&sanadType=11&page=1&pageSize=30
    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] int idSal,
        [FromQuery] int sanadType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
        => Ok(await _documentService.GetHistoryAsync(idSal, sanadType, page, pageSize, cancellationToken));

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
        if (!persisted.Success || persisted.Data == null)
            return StatusCode(500, ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_RESPONSE_LOAD_FAILED", loadError));

        await TrySendBuyerDocumentRegistrationSmsAsync(persisted.Data, ct);

        return StatusCode(201, new ApiResponse<DocumentResponse> { Success = true, Code = result.Code, Message = result.Message, Data = persisted.Data, Errors = result.Errors, Warnings = result.Warnings, TraceId = result.TraceId });
    }


    private async Task TrySendBuyerDocumentRegistrationSmsAsync(
        DocumentResponse document,
        CancellationToken cancellationToken)
    {
        // Send only for sale documents where the Taraf is the buyer.
        if (document.SanadType is not (12 or 15 or 113))
            return;

        var mobile = await _context.Tarafs
            .AsNoTracking()
            .Where(x =>
                x.Id == document.IdTaraf &&
                x.IdType == document.IdTarafType &&
                !x.IsDisabled)
            .Select(x => x.Mobile)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(mobile))
            return;

        // The existing Kavenegar pattern "templatemobile" uses:
        // %token  = invoice/factor number
        // %token3 = next-purchase gift code
        var factorToken = document.IdFaktor.ToString(CultureInfo.InvariantCulture);

        var discountCode = await _context.SmsLogs
            .AsNoTracking()
            .Where(x =>
                x.IdSal == document.IdSal &&
                x.IdSanad == document.Id &&
                x.Message.StartsWith("templatemobile:", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Id)
            .Select(x => x.Message)
            .FirstOrDefaultAsync(cancellationToken);

        var token3 = ExtractTemplateToken(discountCode, "token3");

        if (string.IsNullOrWhiteSpace(token3))
            return;

        try
        {
            await _smsService.SendTemplateAsync(
                mobile: mobile,
                templateName: "templatemobile",
                token: factorToken,
                token3: token3,
                personId: document.IdTaraf,
                idSal: document.IdSal,
                idSanad: document.Id,
                ct: cancellationToken);
        }
        catch
        {
            // SMS failure must not make a successfully registered document fail.
        }
    }

    private static string? ExtractTemplateToken(string? message, string tokenName)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var marker = tokenName + "=";
        var start = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += marker.Length;
        var end = message.IndexOf(';', start);
        return (end < 0 ? message[start..] : message[start..end]).Trim();
    }

    private int? GetCurrentUserId(int? fallback)
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var raw) &&
            int.TryParse(raw.FirstOrDefault(), out var userId) && userId > 0)
            return userId;
        return fallback;
    }
}
