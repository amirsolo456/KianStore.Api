using KianStore.Api.Common;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/stock")]
public sealed class StockController : ControllerBase
{
    private readonly IStockService _stockService;

    public StockController(IStockService stockService)
    {
        _stockService = stockService;
    }

    [HttpGet("{kalaId}")]
    public async Task<IActionResult> GetStock(
        string kalaId,
        [FromQuery] int idAnbar = 1,
        [FromQuery] int idSal = 1405,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kalaId))
        {
            throw new BusinessException(
                "INVALID_PRODUCT_ID",
                "شناسه کالا معتبر نیست.");
        }

        var stock = await _stockService.GetStockAsync(
            kalaId,
            idAnbar,
            idSal,
            cancellationToken);

        return Ok(ApiResponse<object>.SuccessResult(
            new
            {
                kalaId,
                idAnbar,
                idSal,
                stock
            },
            "موجودی کالا با موفقیت دریافت شد."));
    }

    [HttpGet("{kalaId}/check")]
    public async Task<IActionResult> CheckStock(
        string kalaId,
        [FromQuery] decimal quantity,
        [FromQuery] int idAnbar = 1,
        [FromQuery] int idSal = 1405,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kalaId))
        {
            throw new BusinessException(
                "INVALID_PRODUCT_ID",
                "شناسه کالا معتبر نیست.");
        }

        if (quantity <= 0)
        {
            throw new BusinessException(
                "INVALID_QUANTITY",
                "تعداد باید بیشتر از صفر باشد.");
        }

        var result = await _stockService.CheckAsync(
            kalaId,
            quantity,
            idAnbar,
            idSal,
            cancellationToken);

        if (!result.IsAvailable)
        {
            return Conflict(ApiResponse<StockCheckResult>.ErrorResult(
                "INSUFFICIENT_STOCK",
                $"موجودی کالای {kalaId} کافی نیست. موجودی: {result.Available}، درخواست: {result.Requested}.",
                result));
        }

        return Ok(ApiResponse<StockCheckResult>.SuccessResult(
            result,
            "موجودی برای تعداد درخواستی کافی است."));
    }
}
