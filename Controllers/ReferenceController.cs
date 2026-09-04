using KianStore.Api.Common;
using KianStore.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/reference")]
public sealed class ReferenceController : ControllerBase
{
    private readonly KianStoreDbContext _context;

    public ReferenceController(KianStoreDbContext context)
    {
        _context = context;
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        var data = await _context.Anbars.AsNoTracking()
            .Where(x => !x.NoActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                type = x.AnbarType,
                address = x.Address,
                managerId = x.MasoolAnbar,
                marketId = x.IdMarket
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.SuccessResult(data, "انبارها با موفقیت دریافت شدند."));
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts([FromQuery] int? type, CancellationToken ct)
    {
        var query = _context.CheckDefs.AsNoTracking().Where(x => !string.IsNullOrWhiteSpace(x.HesName));
        if (type.HasValue)
            query = query.Where(x => x.Type == type.Value);

        var data = await query
            .OrderBy(x => x.HesName)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                id = x.Id,
                type = x.Type,
                name = x.HesName,
                bank = x.Bank,
                branch = x.Shobeh,
                accountNumber = x.HesabNum,
                balance = x.Mojodi,
                city = x.Shahr,
                description = x.Des
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.SuccessResult(data, "حساب‌ها با موفقیت دریافت شدند."));
    }

    [HttpGet("parties")]
    public async Task<IActionResult> Parties(
        [FromQuery] string? search,
        [FromQuery] int? idType,
        [FromQuery] bool? isBuyer,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Tarafs.AsNoTracking().Where(x => !x.IsDisabled);
        if (idType.HasValue)
            query = query.Where(x => x.IdType == idType.Value);
        if (isBuyer.HasValue)
            query = query.Where(x => x.IsBuyer == isBuyer.Value);

        int? numericId = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            if (int.TryParse(value, out var parsedId))
                numericId = parsedId;

            query = query.Where(x =>
                x.Name.Contains(value) ||
                (x.Mobile != null && x.Mobile.Contains(value)) ||
                (x.Phone != null && x.Phone.Contains(value)) ||
                (numericId.HasValue && x.Id == numericId.Value));
        }

        var data = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.Id,
                idType = x.IdType,
                name = x.Name,
                phone = x.Phone,
                mobile = x.Mobile,
                isBuyer = x.IsBuyer
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.SuccessResult(
            new { page, pageSize, items = data },
            "طرف‌حساب‌ها با موفقیت دریافت شدند."));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        var data = await _context.Users.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new { id = x.Id, idSandogh = x.IdSandogh, idSandoghType = x.IdSandoghType })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.SuccessResult(data, "کاربران با موفقیت دریافت شدند."));
    }

    [HttpGet("document-types")]
    public async Task<IActionResult> DocumentTypes([FromQuery] int idSal = 1405, CancellationToken ct = default)
    {
        var data = await _context.Sanads.AsNoTracking()
            .Where(x => x.IdSal == idSal)
            .GroupBy(x => x.SanadType)
            .Select(x => new { sanadType = x.Key, count = x.Count() })
            .OrderBy(x => x.sanadType)
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.SuccessResult(data, "انواع اسناد ثبت‌شده با موفقیت دریافت شدند."));
    }
}
