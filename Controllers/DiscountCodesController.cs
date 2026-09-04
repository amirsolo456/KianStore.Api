using KianStore.Api.DTOs.DiscountCodes;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/discount-codes")]
public sealed class DiscountCodesController : ControllerBase
{
    private readonly DiscountCodeService _service;

    public DiscountCodesController(DiscountCodeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDiscountCodeRequest request, CancellationToken ct)
        => Ok(await _service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDiscountCodeRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateDiscountCodeRequest request, CancellationToken ct)
        => Ok(await _service.ValidateAsync(request, ct));

    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] ConsumeDiscountCodeRequest request, CancellationToken ct)
        => Ok(await _service.ConsumeAsync(request.Code, request.PersonId, request.OrderAmount, request.IdSal, request.IdSanad, ct));
}

public sealed class ConsumeDiscountCodeRequest
{
    public string Code { get; set; } = null!;
    public int PersonId { get; set; }
    public decimal OrderAmount { get; set; }
    public int? IdSal { get; set; }
    public string? IdSanad { get; set; }
}
