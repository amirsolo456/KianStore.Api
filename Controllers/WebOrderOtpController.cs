using KianStore.Api.Common;
using KianStore.Api.DTOs.WebOrders;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web/order-otp")]
public sealed class WebOrderOtpController : ControllerBase
{
    private readonly OrderOtpService _otp;

    public WebOrderOtpController(OrderOtpService otp)
    {
        _otp = otp;
    }

    [HttpPost("send")]
    public async Task<ActionResult<ApiResponse<OrderOtpResponse>>> Send(
        [FromBody] SendOrderOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _otp.SendAsync(request.Mobile, cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse<OrderOtpResponse>.ErrorResult("OTP_SEND_FAILED", result.Message));

        return Ok(ApiResponse<OrderOtpResponse>.SuccessResult(new OrderOtpResponse
        {
            Challenge = result.Challenge!,
            ExpiresInSeconds = result.ExpiresInSeconds
        }, result.Message));
    }

    [HttpPost("verify")]
    public ActionResult<ApiResponse<object>> Verify([FromBody] VerifyOrderOtpRequest request)
    {
        var result = _otp.Verify(request.Mobile, request.Code, request.Challenge);
        if (!result.Success)
            return BadRequest(ApiResponse<object>.ErrorResult("OTP_VERIFY_FAILED", result.Message));

        return Ok(ApiResponse<object>.SuccessResult(new { verified = true }, result.Message));
    }
}
