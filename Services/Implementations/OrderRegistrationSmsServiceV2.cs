using KianStore.Api.Data;
using KianStore.Api.DTOs.Sms;
using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Services.Implementations;

public sealed class OrderRegistrationSmsServiceV2
{
    private const string TemplateName = "templatemobile";
    private readonly KianStoreDbContext _context;
    private readonly DiscountCodeService _discountCodeService;

    public OrderRegistrationSmsServiceV2(KianStoreDbContext context, DiscountCodeService discountCodeService)
    {
        _context = context;
        _discountCodeService = discountCodeService;
    }

    public async Task<object> SaveResultAsync(OrderRegistrationSmsResultRequest request, CancellationToken ct = default)
    {
        if (request.IdSal <= 0 || string.IsNullOrWhiteSpace(request.IdSanad) || request.PersonId <= 0 || request.FactorNumber <= 0)
            throw new ArgumentException("اطلاعات سند برای ثبت نتیجه پیامک کامل نیست.");

        var sanad = await _context.Sanads
            .FirstOrDefaultAsync(x => x.IdSal == request.IdSal && x.Id == request.IdSanad, ct);

        if (sanad == null)
            throw new KeyNotFoundException("سند مورد نظر یافت نشد.");

        if (sanad.IdTaraf != request.PersonId)
            throw new InvalidOperationException("خریدار سند با خریدار نتیجه پیامک مطابقت ندارد.");

        var mobile = NormalizeMobile(request.Mobile);
        if (!IsValidMobile(mobile))
            throw new ArgumentException("شماره موبایل معتبر نیست.");

        var templateId = await _context.SmsTemplates
            .AsNoTracking()
            .Where(x => x.Name == TemplateName && x.IsActive)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

        var discount = string.IsNullOrWhiteSpace(request.DiscountCode) ? null : request.DiscountCode.Trim();
        var message = $"{TemplateName}: token={request.FactorNumber}; token3={discount ?? string.Empty}";

        var log = await _context.SmsLogs
            .FirstOrDefaultAsync(x =>
                x.IdSal == request.IdSal &&
                x.IdSanad == request.IdSanad &&
                x.Message.StartsWith(TemplateName + ":"), ct);

        if (log == null)
        {
            log = new SmsLog
            {
                PersonId = request.PersonId,
                IdSal = request.IdSal,
                IdSanad = request.IdSanad,
                Mobile = mobile,
                Message = message,
                TemplateId = templateId,
                CreatedAt = DateTime.UtcNow
            };
            _context.SmsLogs.Add(log);
        }

        log.PersonId = request.PersonId;
        log.IdSal = request.IdSal;
        log.IdSanad = request.IdSanad;
        log.Mobile = mobile;
        log.Message = message;
        log.TemplateId = templateId;
        log.Status = request.SmsSent ? 2 : 3;
        log.Provider = string.IsNullOrWhiteSpace(request.Provider) ? "Kavenegar" : request.Provider.Trim();
        log.ProviderMessageId = string.IsNullOrWhiteSpace(request.ProviderMessageId) ? null : request.ProviderMessageId.Trim();
        log.ProviderStatus = request.ProviderStatus;
        log.ProviderStatusText = string.IsNullOrWhiteSpace(request.ProviderStatusText) ? null : request.ProviderStatusText.Trim();
        log.ErrorMessage = request.SmsSent
            ? null
            : (string.IsNullOrWhiteSpace(request.ErrorMessage) ? "ارسال پیامک ناموفق بود." : request.ErrorMessage.Trim());
        log.LastStatusCheckedAt = DateTime.UtcNow;

        sanad.Sharh = MergeSmsStatusIntoSharh(
            sanad.Sharh,
            request.SmsSent,
            request.ProviderMessageId,
            request.ProviderStatus,
            request.ProviderStatusText,
            request.ErrorMessage);

        await _context.SaveChangesAsync(ct);

        return new
        {
            success = true,
            saved = true,
            smsSent = request.SmsSent,
            status = request.SmsSent ? "sent" : "failed",
            statusText = request.SmsSent
                ? (request.ProviderStatusText ?? "نتیجه ارسال پیامک ثبت شد.")
                : (request.ErrorMessage ?? "نتیجه ناموفق پیامک ثبت شد."),
            providerMessageId = request.ProviderMessageId,
            providerStatus = request.ProviderStatus,
            idSal = request.IdSal,
            idSanad = request.IdSanad
        };
    }


    public async Task<object> GetStatusAsync(int idSal, string idSanad, CancellationToken ct = default)
    {
        var sanad = await _context.Sanads.AsNoTracking()
            .Where(x => x.IdSal == idSal && x.Id == idSanad)
            .Select(x => new { x.Id, x.IdFaktor })
            .FirstOrDefaultAsync(ct);

        if (sanad == null)
            return new { smsSent = false, status = "not_found", statusText = "سند یافت نشد." };

        var log = await _context.SmsLogs.AsNoTracking()
            .Where(x => x.IdSal == idSal && x.IdSanad == idSanad && x.Message.StartsWith(TemplateName + ":"))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (log == null)
            return new { smsSent = false, status = "not_sent", statusText = "نتیجه پیامک ثبت نشده است.", factorNumber = sanad.IdFaktor };

        return new
        {
            smsSent = log.Status == 2,
            status = log.Status == 2 ? "sent" : "failed",
            statusText = log.Status == 2
                ? (log.ProviderStatusText ?? "ارسال شد")
                : (log.ErrorMessage ?? "ارسال ناموفق"),
            providerMessageId = log.ProviderMessageId,
            providerStatus = log.ProviderStatus,
            factorNumber = sanad.IdFaktor,
            discountCode = ExtractDiscountCode(log.Message),
            createdAt = log.CreatedAt
        };
    }


    public async Task<List<object>> GetStatusesAsync(int idSal, int sanadType, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var sanadRows = await _context.Sanads.AsNoTracking().Where(x => x.IdSal == idSal && x.SanadType == sanadType && !x.Disable).OrderByDescending(x => x.IdFaktor).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.IdFaktor }).ToListAsync(ct);
        if (sanadRows.Count == 0) return new List<object>();
        var ids = sanadRows.Select(x => x.Id).ToList();
        var rows = await _context.SmsLogs.AsNoTracking().Where(x => x.IdSal == idSal && x.IdSanad != null && ids.Contains(x.IdSanad) && x.Message.StartsWith(TemplateName + ":")).OrderByDescending(x => x.Id).ToListAsync(ct);
        var latest = rows.GroupBy(x => x.IdSanad!).ToDictionary(g => g.Key, g => g.First());
        return sanadRows.Select(s => latest.TryGetValue(s.Id, out var log) ? (object)new { idSanad = s.Id, factorNumber = s.IdFaktor, smsSent = log.Status == 2, status = log.Status == 2 ? "sent" : log.Status == 3 ? "failed" : "pending", statusText = log.Status == 2 ? (log.ProviderStatusText ?? "ارسال شد") : (log.ErrorMessage ?? "در انتظار"), providerMessageId = log.ProviderMessageId, discountCode = ExtractDiscountCode(log.Message) } : new { idSanad = s.Id, factorNumber = s.IdFaktor, smsSent = false, status = "not_sent", statusText = "ارسال نشده", providerMessageId = (string?)null, discountCode = (string?)null }).ToList();
    }

    private static string? ExtractDiscountCode(string message)
    {
        const string marker = "token3=";
        var index = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? null : message[(index + marker.Length)..].Trim();
    }

    private static string NormalizeMobile(string mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0098")) digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length == 12) digits = "0" + digits[2..];
        return digits;
    }

    private static bool IsValidMobile(string mobile) => mobile.Length == 11 && mobile.StartsWith("09") && mobile.All(char.IsDigit);
}
