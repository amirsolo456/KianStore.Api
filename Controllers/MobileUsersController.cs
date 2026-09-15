using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Auth;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/mobile-users")]
public sealed class MobileUsersController : ControllerBase
{
    private readonly MobileAuthService _authService;
    private readonly KianStoreDbContext _db;

    public MobileUsersController(MobileAuthService authService, KianStoreDbContext db)
    {
        _authService = authService;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMobileUserRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var raw) ||
            !int.TryParse(raw.FirstOrDefault(), out var currentUserId) || currentUserId <= 0)
            return BadRequest(ApiResponse<object>.ErrorResult("USER_ID_REQUIRED", "شناسه کاربر ارسال نشده است."));

        var currentUser = await _db.Set<Users>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == currentUserId, ct);

        if (currentUser == null || currentUser.Access != 1)
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
