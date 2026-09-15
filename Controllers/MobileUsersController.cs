using System.Security.Claims;
using KianStore.Api.Common;
using KianStore.Api.DTOs.Auth;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KianStore.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/mobile-users")]
public sealed class MobileUsersController : ControllerBase
{
    private readonly MobileAuthService _authService;

    public MobileUsersController(MobileAuthService authService) => _authService = authService;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMobileUserRequest request, CancellationToken ct)
    {
        var access = int.TryParse(User.FindFirstValue("access"), out var currentAccess) ? currentAccess : 0;
        if (currentAccess != 1)
            return Forbid();

        try
        {
            await _authService.CreateUserAsync(request, ct);
            return Ok(ApiResponse<object>.SuccessResult(
                new { username = request.Username.Trim() },
                "کاربر با موفقیت ایجاد شد."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.ErrorResult("USER_CREATE_FAILED", ex.Message));
        }
    }
}