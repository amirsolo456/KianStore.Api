using KianStore.Api.Common;
using KianStore.Api.DTOs.StockTransfers;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/stock-transfers")]
public sealed class StockTransfersController : ControllerBase
{
    private readonly StockTransferService _service;

    public StockTransfersController(StockTransferService service) => _service = service;

    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses(CancellationToken ct)
        => Ok(await _service.GetWarehousesAsync(ct));

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(
        [FromQuery] int idSal = 1405,
        [FromQuery] int sourceAnbarId = 1,
        CancellationToken ct = default)
        => Ok(await _service.GetInventoryAsync(idSal, sourceAnbarId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] StockTransferRequest request,
        CancellationToken ct)
        => StatusCode(201, await _service.CreateAsync(request, ct));
}
