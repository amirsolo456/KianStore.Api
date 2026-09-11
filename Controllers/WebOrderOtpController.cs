using System.Security.Cryptography;
using KianStore.Api.Common;
using KianStore.Api.DTOs.Sms;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web/order-otp")]
public sealed class WebOrderOtpController : ControllerBase
{
    private const int ExpirationSeconds = 300;
    private const int ResendCooldownSeconds = 60;
    private readonly IMemoryCache _cache;
    private readonly SmsService _smsService;

    public WebOrderOtpController(IMemoryCache cache, SmsService smsService)
    {
        _cache = cache;
        _smsService = smsService;
    }

    [HttpPost("send")]
    public async Task<ActionResult<ApiResponse<object>>> Send([FromBody] WebOrderOtpSendRequest request, CancellationToken ct)
    {
        var mobile = NormalizeMobile(request.Mobile);
        if (!IsValidMobile(mobile))
            return BadRequest(ApiResponse<object>.ErrorResult("INVALID_MOBILE", "شماره موبایل معتبر نیست."));

        var cooldownKey = $"web-order-otp-cooldown:{mobile}";
        if (_cache.TryGetValue(cooldownKey, out _))
            return StatusCode(429, ApiResponse<object>.ErrorResult("OTP_COOLDOWN", "لطفاً برای ارسال مجدد کد کمی صبر کنید."));

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var challenge = Guid.NewGuid().ToString("N");
        _cache.Set($"web-order-otp:{challenge}", new OtpEntry(mobile, code), TimeSpan.FromSeconds(ExpirationSeconds));
        _cache.Set(cooldownKey, true, TimeSpan.FromSeconds(ResendCooldownSeconds));

        var sms = await _smsService.SendAsync(new SendSmsRequest
        {
            Mobile = mobile,
            Message = $"کد تأیید فروشگاه خاتون: {code}\nاین کد تا ۵ دقیقه معتبر است.",
            PersonId = null,
            TemplateId = null
        }, ct);

        if (sms is not null && sms.GetType().GetProperty("success")?.GetValue(sms) is bool success && !success)
        {
            _cache.Remove($"web-order-otp:{challenge}");
            return StatusCode(502, ApiResponse<object>.ErrorResult("OTP_SEND_FAILED", "ارسال کد تأیید انجام نشد."));
        }

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            challenge,
            expiresInSeconds = ExpirationSeconds
        }, "کد تأیید ارسال شد."));
    }

    [HttpPost("verify")]
    public ActionResult<ApiResponse<object>> Verify([FromBody] WebOrderOtpVerifyRequest request)
    {
        var mobile = NormalizeMobile(request.Mobile);
        if (!IsValidMobile(mobile) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Challenge))
            return BadRequest(ApiResponse<object>.ErrorResult("INVALID_OTP_REQUEST", "اطلاعات تأیید ناقص است."));

        var key = $"web-order-otp:{request.Challenge.Trim()}";
        if (!_cache.TryGetValue(key, out OtpEntry? entry) || entry is null)
            return BadRequest(ApiResponse<object>.ErrorResult("OTP_EXPIRED", "کد تأیید منقضی شده است. دوباره درخواست کد کنید."));

        if (!string.Equals(entry.Mobile, mobile, StringComparison.Ordinal))
            return BadRequest(ApiResponse<object>.ErrorResult("OTP_INVALID", "کد تأیید صحیح نیست."));

        if (!string.Equals(entry.Code, request.Code.Trim(), StringComparison.Ordinal))
            return BadRequest(ApiResponse<object>.ErrorResult("OTP_INVALID", "کد تأیید صحیح نیست."));

        _cache.Remove(key);
        return Ok(ApiResponse<object>.SuccessResult(new { verified = true }, "شماره موبایل با موفقیت تأیید شد."));
    }

    private sealed record OtpEntry(string Mobile, string Code);

    private static string NormalizeMobile(string? mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0098")) digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length == 12) digits = "0" + digits[2..];
        return digits;
    }

    private static bool IsValidMobile(string mobile)
        => mobile.Length == 11 && mobile.StartsWith("09") && mobile.All(char.IsDigit);
}

public sealed class WebOrderOtpSendRequest
{
    public string Mobile { get; set; } = null!;
}

public sealed class WebOrderOtpVerifyRequest
{
    public string Mobile { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Challenge { get; set; } = null!;
}
