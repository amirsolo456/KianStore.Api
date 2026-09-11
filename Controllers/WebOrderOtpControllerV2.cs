using KianStore.Api.Common;
using KianStore.Api.DTOs.WebOrders;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web/order-otp")]
public sealed class WebOrderOtpControllerV2 : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebOrderOtpControllerV2(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("send")]
    public async Task<ActionResult<ApiResponse<OrderOtpResponse>>> Send(
        [FromBody] SendOrderOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await WebOrderOtpStore.SendAsync(request.Mobile, _configuration, _httpClientFactory, cancellationToken);
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
        var result = WebOrderOtpStore.Verify(request.Mobile, request.Code, request.Challenge);
        if (!result.Success)
            return BadRequest(ApiResponse<object>.ErrorResult("OTP_VERIFY_FAILED", result.Message));

        return Ok(ApiResponse<object>.SuccessResult(new { verified = true }, result.Message));
    }
}
