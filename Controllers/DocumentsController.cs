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
            return StatusCode(201, result);

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
