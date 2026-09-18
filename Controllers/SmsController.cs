using KianStore.Api.DTOs.Sms;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/sms")]
public sealed class SmsController : ControllerBase
{
    private readonly SmsService _service;
    private readonly OrderRegistrationSmsServiceV2 _orderRegistrationSmsService;

    public SmsController(SmsService service, OrderRegistrationSmsServiceV2 orderRegistrationSmsService)
    {
        _service = service;
        _orderRegistrationSmsService = orderRegistrationSmsService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendSmsRequest request, CancellationToken ct)
        => Ok(await _service.SendAsync(request, ct));

    [HttpGet("logs")]
    public async Task<IActionResult> Logs([FromQuery] int? personId, CancellationToken ct)
        => Ok(await _service.GetLogsAsync(personId, ct));

    [HttpGet("templates")]
    public async Task<IActionResult> Templates([FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await _service.GetTemplatesAsync(activeOnly, ct));

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateSmsTemplateRequest request, CancellationToken ct)
        => Ok(await _service.CreateTemplateAsync(request, ct));

    [HttpPut("templates/{id:int}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] UpdateSmsTemplateRequest request, CancellationToken ct)
    {
        await _service.UpdateTemplateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("order-registration/result")]
    public async Task<IActionResult> SaveOrderRegistrationResult(
        [FromBody] OrderRegistrationSmsResultRequest request,
        CancellationToken ct)
        => Ok(await _orderRegistrationSmsService.SaveResultAsync(request, ct));

    [HttpGet("order-status/{idSal:int}/{idSanad}")]
    public async Task<IActionResult> OrderStatus(int idSal, string idSanad, CancellationToken ct)
        => Ok(await _orderRegistrationSmsService.GetStatusAsync(idSal, idSanad, ct));

    [HttpGet("order-statuses")]
    public async Task<IActionResult> OrderStatuses([FromQuery] int idSal, [FromQuery] int sanadType = 12, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
        => Ok(await _orderRegistrationSmsService.GetStatusesAsync(idSal, sanadType, page, pageSize, ct));
}
