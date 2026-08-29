using Microsoft.AspNetCore.Mvc;
using KianStore.Api.Common;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Models.Orders;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ISanadService _sanadService;

    public OrdersController(IOrderService orderService, ISanadService sanadService)
    {
        _orderService = orderService;
        _sanadService = sanadService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<OrderResponse>>>> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] MobileOrderStatus? status = null)
    {
        var result = await _orderService.GetOrdersAsync(page, pageSize, status);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> GetOrder(long id)
    {
        var result = await _orderService.GetOrderByIdAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> CreateOrder(CreateOrderRequest request)
    {
        var result = await _orderService.CreateOrderAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id}/payments")]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> AddPayment(long id, AddPaymentRequest request)
    {
        var result = await _orderService.AddPaymentAsync(id, request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id}/payments/{paymentId}/verify")]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> VerifyPayment(long id, long paymentId)
    {
        var result = await _orderService.VerifyPaymentAsync(id, paymentId);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> ConfirmOrder(long id)
    {
        var result = await _orderService.ConfirmOrderAsync(id);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id}/convert-to-sanad")]
    public async Task<ActionResult<ApiResponse<string>>> ConvertToSanad(long id)
    {
        var result = await _sanadService.ConvertToSanadAsync(id);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
