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
        return StatusCode(201, result);
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
