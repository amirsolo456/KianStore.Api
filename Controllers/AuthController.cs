using System.Security.Claims;
using KianStore.Api.Common;
using KianStore.Api.DTOs.Auth;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly MobileAuthService _authService;

    public AuthController(MobileAuthService authService) => _authService = authService;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _authService.LoginAsync(request.Username, request.Password, ct);
            if (result == null)
                return Unauthorized(ApiResponse<object>.ErrorResult("INVALID_CREDENTIALS", "نام کاربری یا رمز عبور صحیح نیست."));

            return Ok(ApiResponse<LoginResponse>.SuccessResult(result, "ورود با موفقیت انجام شد."));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("JWT signing key", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.ErrorResult("AUTH_CONFIGURATION_ERROR", "تنظیمات امنیتی سرور کامل نیست."));
        }
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var fullName = User.FindFirstValue("full_name") ?? string.Empty;
        var access = int.TryParse(User.FindFirstValue("access"), out var parsedAccess) ? parsedAccess : 0;

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            userId = int.TryParse(userId, out var id) ? id : 0,
            userName,
            fullName,
            access
        }, "کاربر معتبر است."));
    }
}
