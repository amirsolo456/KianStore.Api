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
            .Where(x => x.Id > 0 && x.IdAnbar > 0)
            .OrderBy(x => x.UserFLName)
            .Select(x => new PurchaseUserResponse
            {
                Id = x.Id,
                Name = x.UserFLName,
                IdAnbar = x.IdAnbar,
                AnbarName = _db.Anbars.Where(a => a.Id == x.IdAnbar).Select(a => a.Name).FirstOrDefault()
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<PurchaseUserResponse>>.SuccessResult(
            users,
            "خریداران داخلی از کاربران دارای انبار دریافت شدند."));
    }

    [HttpPost("purchase-employees")]
    public async Task<IActionResult> CreatePurchaseEmployee(
        [FromBody] CreatePurchaseUserRequest request,
        CancellationToken ct)
    {
        var fullName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName))
            return BadRequest(ApiResponse<object>.ErrorResult("NAME_REQUIRED", "نام خریدار داخلی الزامی است."));

        if (fullName.Length > 60)
            return BadRequest(ApiResponse<object>.ErrorResult("NAME_TOO_LONG", "نام خریدار داخلی نباید بیشتر از 60 کاراکتر باشد."));

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var existing = await _db.Users.AsNoTracking()
                .Where(x => x.UserFLName == fullName && x.IdAnbar > 0)
                .Select(x => new { x.Id, x.UserFLName, x.IdAnbar })
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                var existingAnbarName = await _db.Anbars.AsNoTracking()
                    .Where(x => x.Id == existing.IdAnbar)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync(ct);

                await transaction.CommitAsync(ct);
                return Ok(ApiResponse<PurchaseUserResponse>.SuccessResult(
                    new PurchaseUserResponse
                    {
                        Id = existing.Id,
                        Name = existing.UserFLName,
                        Post = "خریدار داخلی",
                        IdAnbar = existing.IdAnbar,
                        AnbarName = existingAnbarName
                    },
                    "این خریدار داخلی از قبل تعریف شده بود."));
            }

            var newUserId = (await _db.Users.Select(x => (int?)x.Id).MaxAsync(ct) ?? 0) + 1;
            var newAnbarId = (await _db.Anbars.Select(x => (int?)x.Id).MaxAsync(ct) ?? 0) + 1;

            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = parts.Length == 1 ? parts[0] : string.Join(' ', parts[..^1]);
            var lastName = parts.Length == 1 ? "" : parts[^1];
            var userName = $"kh_{newUserId}";
            var password = Guid.NewGuid().ToString("N");

            var anbarName = $"انبار خرید {fullName}";
            var post = "خریدار داخلی";

            var accessType = await _db.Database.SqlQueryRaw<int>(
                "SELECT TOP (1) ID AS [Value] FROM dbo.UserType WHERE ID > 1 ORDER BY ID")
                .FirstOrDefaultAsync(ct);

            if (accessType <= 0)
                throw new ApiException(409, "USER_TYPE_NOT_FOUND", "نوع کاربری مناسب برای خریدار داخلی در دیتابیس پیدا نشد.");

            await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO dbo.Anbar
    (ID, AnabrName, AnbarType, NoActive, MasoolAnbar, ShomareshType, AnbarAddr, IDMarket)
VALUES
    ({newAnbarId}, {anbarName},
     ISNULL((SELECT MIN(ID) FROM dbo.AnbarType), 1),
     0,
     ISNULL((SELECT MIN(ID) FROM dbo.MasoolAnbar), 0),
     ISNULL((SELECT MIN(ID) FROM dbo.ShomareshType), 1),
     NULL,
     ISNULL((SELECT IDMarket FROM dbo.Inf WHERE ID = 1), 0));", ct);

            await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO dbo.Users
    (ID, LName, FName, Post, UserName, Pass, Access, IDAnbar)
VALUES
    ({newUserId}, {lastName}, {firstName}, {post}, {userName}, {password}, {accessType}, {newAnbarId});", ct);

            await transaction.CommitAsync(ct);

            return Ok(ApiResponse<PurchaseUserResponse>.SuccessResult(
                new PurchaseUserResponse
                {
                    Id = newUserId,
                    Name = fullName,
                    Post = post,
                    IdAnbar = newAnbarId,
                    AnbarName = anbarName
                },
                "خریدار داخلی و انبار اختصاصی او با موفقیت ایجاد شد."));
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}

public sealed class CreatePurchaseUserRequest
{
    public string? Name { get; init; }
}

public sealed class PurchaseUserResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Post { get; init; } = string.Empty;
    public int IdAnbar { get; init; }
    public string? AnbarName { get; init; }
}
