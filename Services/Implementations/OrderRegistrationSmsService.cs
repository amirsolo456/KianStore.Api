using System.Net;
using System.Text.Json;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Sms;
using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Services.Implementations;

public sealed class OrderRegistrationSmsService
{
    private const string TemplateName = "templatemobile";
    private readonly KianStoreDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly DiscountCodeService _discountCodeService;

    public OrderRegistrationSmsService(KianStoreDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, DiscountCodeService discountCodeService)
    {
        _context = context; _httpClientFactory = httpClientFactory; _configuration = configuration; _discountCodeService = discountCodeService;
    }

    public async Task<object> SendAsync(OrderRegistrationSmsRequest request, CancellationToken ct = default)
    {
        var mobile = NormalizeMobile(request.Mobile);
        if (!IsValidMobile(mobile)) throw new ArgumentException("شماره موبایل معتبر نیست.");
        if (request.IdSal <= 0 || string.IsNullOrWhiteSpace(request.IdSanad) || request.PersonId <= 0 || request.FactorNumber <= 0) throw new ArgumentException("اطلاعات سند برای ارسال پیامک کامل نیست.");
        var documentExists = await _context.Sanads.AsNoTracking().AnyAsync(x => x.IdSal == request.IdSal && x.Id == request.IdSanad && x.IdTaraf == request.PersonId && !x.Disable, ct);
        if (!documentExists) throw new KeyNotFoundException("سند مورد نظر یافت نشد.");
        var discount = string.IsNullOrWhiteSpace(request.DiscountCode)
            ? await _context.DiscountCodes.AsNoTracking().Where(x => x.PersonId == request.PersonId && x.IssuedForIdSal == request.IdSal && x.IssuedForIdSanad == request.IdSanad && x.IsActive).OrderByDescending(x => x.Id).Select(x => x.Code).FirstOrDefaultAsync(ct)
            : request.DiscountCode!.Trim();
        if (string.IsNullOrWhiteSpace(discount))
        {
            var percent = _configuration.GetValue<decimal?>("DiscountCode:NextPurchasePercentage") ?? 10m;
            var days = _configuration.GetValue<int?>("DiscountCode:NextPurchaseValidDays") ?? 30;
            var issued = await _discountCodeService.IssueNextPurchaseAsync(new DTOs.DiscountCodes.IssueNextPurchaseDiscountRequest { PersonId = request.PersonId, Type = 1, Value = percent, ValidDays = days, PerCustomerLimit = 1, Title = "تخفیف خرید بعدی" }, request.IdSal, request.IdSanad, ct);
            discount = JsonSerializer.SerializeToElement(issued).GetProperty("Code").GetString();
        }
        if (string.IsNullOrWhiteSpace(discount)) throw new InvalidOperationException("کد تخفیف خرید بعدی قابل تولید نبود.");
        var templateId = await _context.SmsTemplates.AsNoTracking().Where(x => x.Name == TemplateName && x.IsActive).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        var log = new SmsLog { PersonId = request.PersonId, IdSal = request.IdSal, IdSanad = request.IdSanad, Mobile = mobile, Message = $"{TemplateName}: token={request.FactorNumber}; token3={discount}", TemplateId = templateId, Status = 1, Provider = "Kavenegar", CreatedAt = DateTime.UtcNow };
        _context.SmsLogs.Add(log); await _context.SaveChangesAsync(ct);
        try
        {
            var result = await SendLookupAsync(mobile, request.FactorNumber.ToString(), discount, ct);
            log.Status = 2; log.ProviderMessageId = result.MessageId; log.ProviderStatus = result.Status; log.ProviderStatusText = result.StatusText; log.LastStatusCheckedAt = DateTime.UtcNow; log.ErrorMessage = null; await _context.SaveChangesAsync(ct);
            return new { success = true, smsSent = true, status = "sent", statusText = result.StatusText ?? "پیامک با موفقیت توسط کاوه‌نگار پذیرفته شد.", providerMessageId = result.MessageId, providerStatus = result.Status, discountCode = discount, factorNumber = request.FactorNumber, template = TemplateName };
        }
        catch (Exception ex)
        {
            log.Status = 3; log.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message; await _context.SaveChangesAsync(ct);
            return new { success = false, smsSent = false, status = "failed", statusText = log.ErrorMessage, providerMessageId = (string?)null, providerStatus = (int?)null, discountCode = discount, factorNumber = request.FactorNumber, template = TemplateName };
        }
    }

    public async Task<object> GetStatusAsync(int idSal, string idSanad, CancellationToken ct = default)
    {
        var factor = await _context.Sanads.AsNoTracking().Where(x => x.IdSal == idSal && x.Id == idSanad).Select(x => (int?)x.IdFaktor).FirstOrDefaultAsync(ct);
        var log = await _context.SmsLogs.AsNoTracking().Where(x => x.IdSal == idSal && x.IdSanad == idSanad && x.Message.StartsWith(TemplateName + ":")).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (log == null) return new { smsSent = false, status = "not_sent", statusText = "برای این سند پیامک ثبت سفارش ارسال نشده است.", factorNumber = factor };
        if (log.Status == 2 && !string.IsNullOrWhiteSpace(log.ProviderMessageId))
        {
            try
            {
                var provider = await GetProviderStatusAsync(log.ProviderMessageId!, ct);
                await _context.SmsLogs.Where(x => x.Id == log.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ProviderStatus, provider.Status).SetProperty(x => x.ProviderStatusText, provider.StatusText).SetProperty(x => x.LastStatusCheckedAt, DateTime.UtcNow), ct);
                log.ProviderStatus = provider.Status; log.ProviderStatusText = provider.StatusText;
            }
            catch { }
        }
        return new { smsSent = log.Status == 2, status = log.Status == 2 ? "sent" : log.Status == 3 ? "failed" : "pending", statusText = log.Status == 2 ? (log.ProviderStatusText ?? "ارسال شد") : log.Status == 3 ? (log.ErrorMessage ?? "ارسال ناموفق") : "در حال ارسال", providerMessageId = log.ProviderMessageId, providerStatus = log.ProviderStatus, factorNumber = factor, discountCode = ExtractDiscountCode(log.Message), createdAt = log.CreatedAt };
    }

    public async Task<object> GetStatusesAsync(int idSal, int sanadType, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var sanadRows = await _context.Sanads.AsNoTracking().Where(x => x.IdSal == idSal && x.SanadType == sanadType && !x.Disable).OrderByDescending(x => x.IdFaktor).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.IdFaktor }).ToListAsync(ct);
        if (sanadRows.Count == 0) return Array.Empty<object>();
        var ids = sanadRows.Select(x => x.Id).ToList();
        var rows = await _context.SmsLogs.AsNoTracking().Where(x => x.IdSal == idSal && x.IdSanad != null && ids.Contains(x.IdSanad) && x.Message.StartsWith(TemplateName + ":")).OrderByDescending(x => x.Id).ToListAsync(ct);
        var latest = rows.GroupBy(x => x.IdSanad!).ToDictionary(g => g.Key, g => g.First());
        return sanadRows.Select(s => latest.TryGetValue(s.Id, out var log) ? (object)new { idSanad = s.Id, factorNumber = s.IdFaktor, smsSent = log.Status == 2, status = log.Status == 2 ? "sent" : log.Status == 3 ? "failed" : "pending", statusText = log.Status == 2 ? (log.ProviderStatusText ?? "ارسال شد") : (log.ErrorMessage ?? "در انتظار"), providerMessageId = log.ProviderMessageId, discountCode = ExtractDiscountCode(log.Message) } : new { idSanad = s.Id, factorNumber = s.IdFaktor, smsSent = false, status = "not_sent", statusText = "ارسال نشده", providerMessageId = (string?)null, discountCode = (string?)null }).ToArray();
    }

    private static string? ExtractDiscountCode(string message)
    {
        const string marker = "token3=";
        var index = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? null : message[(index + marker.Length)..].Trim();
    }

    private async Task<(string? MessageId, int? Status, string? StatusText)> SendLookupAsync(string mobile, string token, string token2, CancellationToken ct)
    {
        var apiKey = _configuration["Sms:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("کلید API کاوه‌نگار تنظیم نشده است.");
        var url = $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/verify/lookup.json";
        var values = new Dictionary<string, string> { ["receptor"] = mobile, ["token"] = token, ["token2"] = token2, ["template"] = TemplateName };
        var client = _httpClientFactory.CreateClient("SmsProvider");
        using var response = await client.PostAsync(url, new FormUrlEncodedContent(values), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException($"خطای کاوه‌نگار: {(int)response.StatusCode} {body}");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var ret = root.GetProperty("return");
        var apiStatus = ret.GetProperty("status").GetInt32();
        var apiMessage = ret.TryGetProperty("message", out var msg) ? msg.GetString() : null;
        if (apiStatus != 200) throw new InvalidOperationException($"کاوه‌نگار: {apiMessage ?? "خطای نامشخص"} (کد {apiStatus})");
        return ParseEntry(root, apiMessage);
    }

    private async Task<(int? Status, string? StatusText)> GetProviderStatusAsync(string messageId, CancellationToken ct)
    {
        var apiKey = _configuration["Sms:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("کلید API کاوه‌نگار تنظیم نشده است.");
        var url = $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/sms/status.json?messageid={Uri.EscapeDataString(messageId)}";
        var client = _httpClientFactory.CreateClient("SmsProvider");
        using var response = await client.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(body);
        using var json = JsonDocument.Parse(body);
        var ret = json.RootElement.GetProperty("return");
        if (ret.GetProperty("status").GetInt32() != 200) throw new InvalidOperationException(ret.GetProperty("message").GetString() ?? "خطای وضعیت پیامک");
        var entry = ParseEntry(json.RootElement, null);
        return (entry.Status, entry.StatusText);
    }

    private static (string? MessageId, int? Status, string? StatusText) ParseEntry(JsonElement root, string? fallback)
    {
        if (!root.TryGetProperty("entries", out var entries)) return (null, null, fallback);
        JsonElement first;
        if (entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0) first = entries[0];
        else if (entries.ValueKind == JsonValueKind.Object) first = entries;
        else return (null, null, fallback);
        var messageId = first.TryGetProperty("messageid", out var id) ? id.ToString() : null;
        var status = first.TryGetProperty("status", out var st) && st.TryGetInt32(out var i) ? i : (int?)null;
        var text = first.TryGetProperty("statustext", out var tx) ? tx.GetString() : fallback;
        return (messageId, status, text);
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
