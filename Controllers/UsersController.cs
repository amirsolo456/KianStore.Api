using KianStore.Api.Common;
using KianStore.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly KianStoreDbContext _db;

    public UsersController(KianStoreDbContext db) => _db = db;

    [HttpGet("purchase-employees")]
    public async Task<IActionResult> GetPurchaseEmployees(CancellationToken ct)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id > 0)
            .OrderBy(x => x.UserFLName)
            .Select(x => new PurchaseUserResponse
            {
                Id = x.Id,
                Name = x.UserFLName,
                Post = x.Post
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<PurchaseUserResponse>>.SuccessResult(
            users,
            "کارکنان از فهرست کاربران با موفقیت دریافت شدند."));
    }
}

public sealed class PurchaseUserResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Post { get; init; } = string.Empty;
}
