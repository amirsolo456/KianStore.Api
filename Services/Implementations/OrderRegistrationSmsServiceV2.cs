using KianStore.Api.Data;
using KianStore.Api.DTOs.Sms;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Services.Implementations;

public sealed class OrderRegistrationSmsServiceV2
{
    public const string SuccessStatus = "success";
    public const string FailedStatus = "failed";

    private readonly KianStoreDbContext _context;

    public OrderRegistrationSmsServiceV2(KianStoreDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The mobile application sends the SMS through Kavenegar directly.
    /// This endpoint only persists the result on the related Sanad row.
    /// It never calls Kavenegar and never sends an SMS.
    /// </summary>
    public async Task<object> SendAsync(
        DocumentSmsStatusRequest request,
        CancellationToken ct = default)
    {
        if (request.IdSal <= 0 || string.IsNullOrWhiteSpace(request.IdSanad))
            throw new ArgumentException("شناسه سند برای ثبت وضعیت پیامک معتبر نیست.");

        var sanad = await _context.Sanads.FirstOrDefaultAsync(
            x => x.IdSal == request.IdSal && x.Id == request.IdSanad,
            ct);

        if (sanad == null)
            throw new KeyNotFoundException("سند مورد نظر یافت نشد.");

        sanad.SmsStatus = request.SmsSent ? SuccessStatus : FailedStatus;
        await _context.SaveChangesAsync(ct);

        return new
        {
            success = true,
            idSal = request.IdSal,
            idSanad = request.IdSanad,
            smsStatus = sanad.SmsStatus
        };
    }

    public async Task<object> GetStatusAsync(
        int idSal,
        string idSanad,
        CancellationToken ct = default)
    {
        var sanad = await _context.Sanads
            .AsNoTracking()
            .Where(x => x.IdSal == idSal && x.Id == idSanad)
            .Select(x => new
            {
                x.IdSal,
                x.Id,
                x.IdFaktor,
                x.SmsStatus
            })
            .FirstOrDefaultAsync(ct);

        if (sanad == null)
            throw new KeyNotFoundException("سند مورد نظر یافت نشد.");

        return new
        {
            idSal = sanad.IdSal,
            idSanad = sanad.Id,
            factorNumber = sanad.IdFaktor,
            smsSent = sanad.SmsStatus == SuccessStatus,
            status = sanad.SmsStatus ?? "not_sent",
            statusText = sanad.SmsStatus == SuccessStatus
                ? "ارسال موفق"
                : sanad.SmsStatus == FailedStatus
                    ? "ارسال ناموفق"
                    : "ارسال نشده"
        };
    }

    public async Task<List<object>> GetStatusesAsync(
        int idSal,
        int sanadType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var rows = await _context.Sanads
            .AsNoTracking()
            .Where(x =>
                x.IdSal == idSal &&
                x.SanadType == sanadType &&
                !x.Disable)
            .OrderByDescending(x => x.IdFaktor)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                idSanad = x.Id,
                factorNumber = x.IdFaktor,
                smsStatus = x.SmsStatus
            })
            .ToListAsync(ct);

        return rows.Select(x => (object)new
        {
            x.idSanad,
            x.factorNumber,
            smsSent = x.smsStatus == SuccessStatus,
            status = x.smsStatus ?? "not_sent",
            statusText = x.smsStatus == SuccessStatus
                ? "ارسال موفق"
                : x.smsStatus == FailedStatus
                    ? "ارسال ناموفق"
                    : "ارسال نشده",
            providerMessageId = (string?)null,
            discountCode = (string?)null
        }).ToList();
    }
}
