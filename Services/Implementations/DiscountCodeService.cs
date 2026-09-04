using KianStore.Api.Data;
using KianStore.Api.DTOs.DiscountCodes;
using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Services.Implementations;

public sealed class DiscountCodeService
{
    private readonly KianStoreDbContext _context;

    public DiscountCodeService(KianStoreDbContext context) => _context = context;

    public async Task<IReadOnlyList<object>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _context.DiscountCodes.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Join(_context.Takhfifs, c => c.TakhfifId, t => t.Id, (c, t) => new
            {
                c.Id, c.Code, c.Title, c.Type, c.Value,
                MinOrderAmount = (decimal?)t.ToMab1,
                c.MaxDiscountAmount, c.StartDate, c.EndDate,
                c.UsageLimit, c.UsedCount, c.PerCustomerLimit,
                c.IsActive, c.Description, c.CreatedAt
            })
            .ToListAsync(ct);
        return rows.Cast<object>().ToList();
    }

    public async Task<object> CreateAsync(CreateDiscountCodeRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request.Code, request.Type, request.Value, request.StartDate, request.EndDate, request.MaxDiscountAmount);
        var code = Normalize(request.Code);
        if (await _context.DiscountCodes.AnyAsync(x => x.Code == code, ct))
            throw new InvalidOperationException("این کد تخفیف قبلاً ثبت شده است.");

        await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var takhfifId = (await _context.Takhfifs.MaxAsync(x => (int?)x.Id, ct) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var takhfifDarsad = request.Type == 1 ? (double)request.Value : 0d;
        var minOrderAmount = request.MinOrderAmount ?? 0m;
        var takhfifName = Truncate(request.Title ?? code, 20);

        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Takhfif]
            ([ID],[TakhfifName],[TakhfifDarsad],[ToMab1],[TakhfifDarsad2],[ToMab2],
             [SumType],[ByTakhfifKala],[Pelekani],[IDHyperMarket],[IdKalaListEx],[IdKalaListOnly],
             [ApplyType],[TasviehType],[IsDisabe],[IDUser],[OrderIndex])
            VALUES
            ({takhfifId},{takhfifName},{takhfifDarsad},{minOrderAmount},0,0,
             0,0,0,0,0,0,0,0,0,0,0)
            """, ct);

        var entity = new DiscountCode
        {
            Code = code,
            Title = request.Title,
            TakhfifId = takhfifId,
            Type = request.Type,
            Value = request.Value,
            MaxDiscountAmount = request.MaxDiscountAmount,
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate?.ToUniversalTime(),
            UsageLimit = request.UsageLimit,
            UsedCount = 0,
            PerCustomerLimit = request.PerCustomerLimit,
            IsActive = request.IsActive,
            Description = request.Description,
            CreatedAt = now
        };

        _context.DiscountCodes.Add(entity);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetByIdAsync(entity.Id, ct) ?? throw new InvalidOperationException("کد تخفیف ایجاد شد اما قابل بازیابی نیست.");
    }

    public async Task UpdateAsync(int id, UpdateDiscountCodeRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request.Code, request.Type, request.Value, request.StartDate, request.EndDate, request.MaxDiscountAmount);
        var entity = await _context.DiscountCodes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("کد تخفیف یافت نشد.");
        var code = Normalize(request.Code);
        if (await _context.DiscountCodes.AnyAsync(x => x.Id != id && x.Code == code, ct))
            throw new InvalidOperationException("این کد تخفیف قبلاً ثبت شده است.");

        entity.Code = code;
        entity.Title = request.Title;
        entity.Type = request.Type;
        entity.Value = request.Value;
        entity.MaxDiscountAmount = request.MaxDiscountAmount;
        entity.StartDate = request.StartDate.ToUniversalTime();
        entity.EndDate = request.EndDate?.ToUniversalTime();
        entity.UsageLimit = request.UsageLimit;
        entity.PerCustomerLimit = request.PerCustomerLimit;
        entity.IsActive = request.IsActive;
        entity.Description = request.Description;

        var takhfif = await _context.Takhfifs.FirstAsync(x => x.Id == entity.TakhfifId, ct);
        takhfif.TakhfifDarsad = request.Type == 1 ? (double)request.Value : 0d;
        takhfif.ToMab1 = request.MinOrderAmount ?? 0m;
        takhfif.TakhfifName = Truncate(request.Title ?? code, 20);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.DiscountCodes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("کد تخفیف یافت نشد.");
        entity.IsActive = false;
        var takhfif = await _context.Takhfifs.FirstOrDefaultAsync(x => x.Id == entity.TakhfifId, ct);
        if (takhfif != null) takhfif.IsDisabe = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<object> ValidateAsync(ValidateDiscountCodeRequest request, CancellationToken ct = default)
    {
        var code = Normalize(request.Code);
        var entity = await _context.DiscountCodes.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
        if (entity == null) return Result(false, 0, request.OrderAmount, "کد تخفیف نامعتبر است.");
        var now = DateTime.UtcNow;
        if (!entity.IsActive) return Result(false, 0, request.OrderAmount, "کد تخفیف غیرفعال است.");
        if (now < entity.StartDate || (entity.EndDate.HasValue && now > entity.EndDate.Value)) return Result(false, 0, request.OrderAmount, "کد تخفیف در بازه زمانی مجاز نیست.");
        if (entity.UsageLimit.HasValue && entity.UsedCount >= entity.UsageLimit.Value) return Result(false, 0, request.OrderAmount, "ظرفیت مصرف کد تخفیف تمام شده است.");
        if (request.OrderAmount <= 0) return Result(false, 0, request.OrderAmount, "مبلغ سفارش معتبر نیست.");

        var takhfif = await _context.Takhfifs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.TakhfifId, ct);
        var minOrder = takhfif?.ToMab1 ?? 0m;
        if (request.OrderAmount < minOrder) return Result(false, 0, request.OrderAmount, $"حداقل مبلغ سفارش برای این کد {minOrder:N0} است.");

        if (entity.PerCustomerLimit.HasValue)
        {
            var customerUsed = await _context.DiscountCodeUsages.CountAsync(x => x.DiscountCodeId == entity.Id && x.PersonId == request.PersonId, ct);
            if (customerUsed >= entity.PerCustomerLimit.Value) return Result(false, 0, request.OrderAmount, "حد مصرف این کد برای این مشتری تمام شده است.");
        }

        var discount = entity.Type == 2 ? entity.Value : request.OrderAmount * entity.Value / 100m;
        if (entity.MaxDiscountAmount.HasValue) discount = Math.Min(discount, entity.MaxDiscountAmount.Value);
        discount = Math.Clamp(discount, 0, request.OrderAmount);
        return Result(true, discount, request.OrderAmount - discount, "کد تخفیف معتبر است.");
    }

    public async Task<object> ConsumeAsync(string code, int personId, decimal orderAmount, int? idSal, string? idSanad, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var entity = await _context.DiscountCodes.FirstOrDefaultAsync(x => x.Code == Normalize(code), ct)
            ?? throw new KeyNotFoundException("کد تخفیف نامعتبر است.");
        var validation = await ValidateAsync(new ValidateDiscountCodeRequest { Code = code, PersonId = personId, OrderAmount = orderAmount }, ct);
        var json = System.Text.Json.JsonSerializer.Serialize(validation);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<ValidationResult>(json)!;
        if (!parsed.IsValid) throw new InvalidOperationException(parsed.Message);

        entity.UsedCount++;
        _context.DiscountCodeUsages.Add(new DiscountCodeUsage
        {
            DiscountCodeId = entity.Id, PersonId = personId, OrderAmount = orderAmount,
            DiscountAmount = parsed.DiscountAmount, IdSal = idSal, IdSanad = idSanad, UsedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return parsed;
    }

    private async Task<object?> GetByIdAsync(int id, CancellationToken ct)
        => await _context.DiscountCodes.AsNoTracking().Where(c => c.Id == id).Join(_context.Takhfifs, c => c.TakhfifId, t => t.Id, (c, t) => new
        {
            c.Id, c.Code, c.Title, c.Type, c.Value, MinOrderAmount = (decimal?)t.ToMab1, c.MaxDiscountAmount,
            c.StartDate, c.EndDate, c.UsageLimit, c.UsedCount, c.PerCustomerLimit, c.IsActive, c.Description, c.CreatedAt
        }).FirstOrDefaultAsync(ct);

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    private static void ValidateRequest(string code, int type, decimal value, DateTime start, DateTime? end, decimal? max)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50) throw new ArgumentException("کد تخفیف باید بین 1 تا 50 کاراکتر باشد.");
        if (type is < 1 or > 2) throw new ArgumentException("نوع تخفیف نامعتبر است.");
        if (value <= 0 || (type == 1 && value > 100)) throw new ArgumentException("مقدار تخفیف نامعتبر است.");
        if (end.HasValue && end.Value < start) throw new ArgumentException("تاریخ پایان نمی‌تواند قبل از شروع باشد.");
        if (max.HasValue && max.Value < 0) throw new ArgumentException("سقف تخفیف نامعتبر است.");
    }
    private static object Result(bool valid, decimal discount, decimal finalAmount, string message) => new { isValid = valid, discountAmount = discount, finalAmount, message };
    private sealed class ValidationResult { public bool IsValid { get; set; } public decimal DiscountAmount { get; set; } public decimal FinalAmount { get; set; } public string Message { get; set; } = ""; }
}
