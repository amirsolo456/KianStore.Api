using KianStore.Api.DTOs.Sms;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/sms")]
public sealed class SmsController : ControllerBase
{
    private readonly SmsService _service;

    public SmsController(SmsService service) => _service = service;

    [HttpGet("status")]
    public async Task<IActionResult> Status()
        => Ok(await _service.GetConfigurationStatusAsync());

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
}
