//using KianStore.Api.Common;
//using KianStore.Api.Services.Interfaces;
//using Microsoft.AspNetCore.Mvc;

//namespace KianStore.Api.Controllers;

//[ApiController]
//[Route("api/stock")]
//public sealed class StockController : ControllerBase
//{
//    private readonly IStockService _stockService;

//    public StockController(IStockService stockService)
//    {
//        _stockService = stockService;
//    }

//    [HttpGet("{kalaId}")]
//    public async Task<IActionResult> GetStock(
//        string kalaId,
//        [FromQuery] int idAnbar = 1,
//        [FromQuery] int idSal = 1405,
//        CancellationToken cancellationToken = default)
//    {
//        if (string.IsNullOrWhiteSpace(kalaId))
//        {
//            throw new BusinessException(
//                "INVALID_PRODUCT_ID",
//                "شناسه کالا معتبر نیست.");
//        }

//        var stock = await _stockService.GetStockAsync(
//            kalaId,
//            idAnbar,
//            idSal,
//            cancellationToken);

//        return Ok(
//            ApiResponse<object>.SuccessResult(
//                new
//                {
//                    kalaId,
//                    idAnbar,
//                    idSal,
//                    stock
//                },
//                message: "موجودی کالا با موفقیت دریافت شد."));
//    }
//}