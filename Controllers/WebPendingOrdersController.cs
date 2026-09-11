using KianStore.Api.Common;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web-orders")]
public sealed class WebPendingOrdersController : ControllerBase
{
    private readonly PendingWebOrderService _service;

    public WebPendingOrdersController(PendingWebOrderService service)
    {
        _service = service;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<object>>> GetPending(CancellationToken cancellationToken)
    {
        var data = await _service.GetPendingAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResult(data, "فاکتورهای وب در انتظار تأیید دریافت شد."));
    }

    [HttpPost("{orderNumber}/finalize")]
    public async Task<ActionResult<ApiResponse<object>>> Finalize(
        string orderNumber,
        [FromBody] WebPendingOrderFinalizeRequest request,
        CancellationToken cancellationToken)
    {
        await _service.FinalizeAsync(orderNumber, request, cancellationToken);
        return Ok(ApiResponse<object>.SuccessResult(
            new { orderNumber },
            "سفارش وب با موفقیت تأیید و نهایی شد."));
    }
}
