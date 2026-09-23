using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.Models.KianStore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/purchase-employees")]
public sealed class PurchaseEmployeesController : ControllerBase
{
    private readonly KianStoreDbContext _db;

    public PurchaseEmployeesController(KianStoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _db.PurchaseEmployees.AsNoTracking();
        if (activeOnly) query = query.Where(x => x.IsActive);

        var data = await query.OrderBy(x => x.Name).Select(x => new PurchaseEmployeeResponse
        {
            Id = x.Id,
            Name = x.Name,
            Mobile = x.Mobile,
            IsActive = x.IsActive
        }).ToListAsync(ct);

        return Ok(ApiResponse<List<PurchaseEmployeeResponse>>.SuccessResult(data, "کارکنان خریدار با موفقیت دریافت شدند."));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PurchaseEmployeeRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<object>.ErrorResult("INVALID_NAME", "نام کارمند را وارد کنید."));

        var employee = new PurchaseEmployee
        {
            Name = name,
            Mobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim(),
            IsActive = true
        };
        _db.PurchaseEmployees.Add(employee);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<PurchaseEmployeeResponse>.SuccessResult(Map(employee), "کارمند خریدار با موفقیت ثبت شد."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PurchaseEmployeeRequest request, CancellationToken ct)
    {
        var employee = await _db.PurchaseEmployees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (employee == null)
            return NotFound(ApiResponse<object>.ErrorResult("PURCHASE_EMPLOYEE_NOT_FOUND", "کارمند خریدار پیدا نشد."));

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<object>.ErrorResult("INVALID_NAME", "نام کارمند را وارد کنید."));

        employee.Name = name;
        employee.Mobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim();
        if (request.IsActive.HasValue) employee.IsActive = request.IsActive.Value;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<PurchaseEmployeeResponse>.SuccessResult(Map(employee), "اطلاعات کارمند خریدار ویرایش شد."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Disable(int id, CancellationToken ct)
    {
        var employee = await _db.PurchaseEmployees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (employee == null)
            return NotFound(ApiResponse<object>.ErrorResult("PURCHASE_EMPLOYEE_NOT_FOUND", "کارمند خریدار پیدا نشد."));

        employee.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<PurchaseEmployeeResponse>.SuccessResult(Map(employee), "کارمند خریدار غیرفعال شد."));
    }

    private static PurchaseEmployeeResponse Map(PurchaseEmployee employee) => new()
    {
        Id = employee.Id,
        Name = employee.Name,
        Mobile = employee.Mobile,
        IsActive = employee.IsActive
    };
}

public sealed class PurchaseEmployeeRequest
{
    public string? Name { get; set; }
    public string? Mobile { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class PurchaseEmployeeResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Mobile { get; init; }
    public bool IsActive { get; init; }
}
