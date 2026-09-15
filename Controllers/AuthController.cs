using Microsoft.AspNetCore.Mvc;
using KianStore.Api.Common;
using KianStore.Api.DTOs.Auth;
using KianStore.Api.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using KianStore.Api.Data;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly MobileAuthService _authService;
    private readonly KianStoreDbContext _db;

    public AuthController(MobileAuthService authService, KianStoreDbContext db)
    {
        _authService = authService;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password, ct);
        if (result == null)
            return Unauthorized(ApiResponse<object>.ErrorResult("INVALID_CREDENTIALS", "نام کاربری یا رمز عبور صحیح نیست."));

        return Ok(ApiResponse<LoginResponse>.SuccessResult(result, "ورود با موفقیت انجام شد."));
    }

    // Optional compatibility endpoint. It uses only the locally stored user id;
    // username/password and bearer tokens are never required after login.
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var raw) ||
            !int.TryParse(raw.FirstOrDefault(), out var userId) || userId <= 0)
            return BadRequest(ApiResponse<object>.ErrorResult("USER_ID_REQUIRED", "شناسه کاربر ارسال نشده است."));

        var user = await _db.Set<KianStore.Api.Models.KianStore.Users>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user == null)
            return NotFound(ApiResponse<object>.ErrorResult("USER_NOT_FOUND", "کاربر یافت نشد."));

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            userId = user.Id,
            userName = user.UserName,
            fullName = user.UserFLName ?? string.Empty,
            access = user.Access
        }, "کاربر معتبر است."));
    }
}
