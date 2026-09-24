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

    [HttpGet("engine")]
    public IActionResult GetEngine()
        => Ok(ApiResponse<object>.SuccessResult(
            new
            {
                Engine = StockTransferService.EngineVersion,
                SourceSanadType = 6,
                DestinationSanadType = 7,
                Procedure = "dbo.InsertTwoSanadRelated",
                CustomSanadType114 = false
            },
            "موتور انتقال موجودی نسخه 6/7 است."));

    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses(CancellationToken ct)
        => Ok(await _service.GetWarehousesAsync(ct));

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(
        [FromQuery] int idSal = 1405,
        [FromQuery] int sourceAnbarId = 1,
        CancellationToken ct = default)
        => Ok(await _service.GetInventoryAsync(idSal, sourceAnbarId, ct));

    [HttpGet("product-inventory")]
    public async Task<IActionResult> GetProductInventory(
        [FromQuery] int idSal = 1405,
        [FromQuery] string idKala = "",
        CancellationToken ct = default)
        => Ok(await _service.GetProductInventoryByWarehousesAsync(idSal, idKala, ct));

    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] int idSal = 1405,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _service.GetHistoryAsync(idSal, page, pageSize, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] StockTransferRequest request,
        CancellationToken ct)
        => StatusCode(201, await _service.CreateAsync(request, ct));

    [HttpPut("{idSal:int}/{id}")]
    public async Task<IActionResult> Update(
        [FromRoute] int idSal,
        [FromRoute] string id,
        [FromBody] UpdateStockTransferRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(idSal, id, request, ct));

    [HttpDelete("{idSal:int}/{id}")]
    public async Task<IActionResult> Delete(
        [FromRoute] int idSal,
        [FromRoute] string id,
        CancellationToken ct)
        => Ok(await _service.DeleteAsync(idSal, id, ct));
}
