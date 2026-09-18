using KianStore.Api.DTOs.Documents;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/documents/sale")]
public sealed class SaleDocumentsController : ControllerBase
{
    private readonly SaleDocumentMutationService _service;

    public SaleDocumentsController(SaleDocumentMutationService service) => _service = service;

    [HttpPut("{idSal:int}/{id}")]
    public async Task<IActionResult> Update(
        int idSal,
        string id,
        [FromBody] UpdateSaleDocumentRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(idSal, id, request, GetCurrentUserId(), cancellationToken));

    [HttpDelete("{idSal:int}/{id}")]
    public async Task<IActionResult> Delete(
        int idSal,
        string id,
        [FromBody] DeleteSaleDocumentRequest? request,
        CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(idSal, id, GetCurrentUserId(), request?.Password, cancellationToken));

    private int? GetCurrentUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var raw) &&
            int.TryParse(raw.FirstOrDefault(), out var userId) && userId > 0)
            return userId;
        return null;
    }
}
