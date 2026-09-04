using System.Net;
using System.Text.Json;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Sms;
using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Services.Implementations;

public sealed class SmsService
{
    private readonly KianStoreDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SmsService(KianStoreDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<object> SendAsync(SendSmsRequest request, CancellationToken ct = default)
    {
        var mobile = NormalizeMobile(request.Mobile);
        if (!IsValidMobile(mobile))
            throw new ArgumentException("شماره موبایل معتبر نیست.");
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("متن پیامک خالی است.");

        int? templateId = request.TemplateId;
        if (templateId.HasValue && !await _context.SmsTemplates.AnyAsync(x => x.Id == templateId && x.IsActive, ct))
            throw new KeyNotFoundException("قالب پیامک یافت نشد یا غیرفعال است.");

        var log = new SmsLog
        {
            PersonId = request.PersonId,
            Mobile = mobile,
            Message = request.Message.Trim(),
            TemplateId = templateId,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.SmsLogs.Add(log);
        await _context.SaveChangesAsync(ct);

        try
        {
            var result = await SendToProviderAsync(mobile, request.Message.Trim(), ct);
            log.Status = 2;
            log.Provider = result.Provider;
            log.ProviderMessageId = result.ProviderMessageId;
            log.ErrorMessage = null;
            await _context.SaveChangesAsync(ct);

            return new
            {
                success = true,
                message = "پیامک با موفقیت ارسال شد.",
                providerMessageId = result.ProviderMessageId
            };
        }
        catch (Exception ex)
        {
            log.Status = 3;
            log.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await _context.SaveChangesAsync(ct);

            return new
            {
                success = false,
                message = log.ErrorMessage,
                providerMessageId = (string?)null
            };
        }
    }

    public async Task<IReadOnlyList<object>> GetLogsAsync(int? personId = null, CancellationToken ct = default)
    {
        var query = _context.SmsLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt).AsQueryable();
        if (personId.HasValue)
            query = query.Where(x => x.PersonId == personId.Value).OrderByDescending(x => x.CreatedAt);

        return await query.Select(x => new
        {
            id = x.Id,
            personId = x.PersonId,
            mobile = x.Mobile,
            message = x.Message,
            status = x.Status,
            provider = x.Provider,
            providerMessageId = x.ProviderMessageId,
            errorMessage = x.ErrorMessage,
            createdAt = x.CreatedAt
        }).Take(200).Cast<object>().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<object>> GetTemplatesAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _context.SmsTemplates.AsNoTracking().OrderByDescending(x => x.Id).AsQueryable();
        if (activeOnly)
            query = query.Where(x => x.IsActive).OrderByDescending(x => x.Id);

        return await query.Select(x => new
        {
            x.Id,
            x.Name,
            x.TemplateText,
            x.IsActive,
            x.CreatedAt,
            x.UpdatedAt
        }).Cast<object>().ToListAsync(ct);
    }

    public async Task<object> CreateTemplateAsync(CreateSmsTemplateRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TemplateText))
            throw new ArgumentException("نام و متن قالب پیامک اجباری است.");

        var entity = new SmsTemplate
        {
            Name = request.Name.Trim(),
            TemplateText = request.TemplateText.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.SmsTemplates.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateTemplateAsync(int id, UpdateSmsTemplateRequest request, CancellationToken ct = default)
    {
        var entity = await _context.SmsTemplates.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("قالب پیامک یافت نشد.");

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TemplateText))
            throw new ArgumentException("نام و متن قالب پیامک اجباری است.");

        entity.Name = request.Name.Trim();
        entity.TemplateText = request.TemplateText.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    private async Task<(string Provider, string? ProviderMessageId)> SendToProviderAsync(
        string mobile,
        string message,
        CancellationToken ct)
    {
        const string providerName = "Kavenegar";
        var provider = (_configuration["Sms:Provider"] ?? providerName).Trim();
        var apiKey = _configuration["Sms:ApiKey"]?.Trim();
        var sender = _configuration["Sms:Sender"]?.Trim();
        var configuredUrl = _configuration["Sms:SendUrl"]?.Trim();

        if (provider.Equals(providerName, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("کلید API کاوه‌نگار تنظیم نشده است. مقدار Sms:ApiKey را در Environment یا تنظیمات سرور قرار دهید.");

            var url = !string.IsNullOrWhiteSpace(configuredUrl)
                ? configuredUrl
                : $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/sms/send.json";

            var values = new Dictionary<string, string>
            {
                ["receptor"] = mobile,
                ["message"] = message
            };

            // sender در کاوه‌نگار می‌تواند از پنل پیش‌فرض استفاده کند؛ فقط در صورت تنظیم ارسال می‌شود.
            if (!string.IsNullOrWhiteSpace(sender))
                values["sender"] = sender;

            var client = _httpClientFactory.CreateClient("SmsProvider");
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(values)
            };

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"خطای ارتباط با کاوه‌نگار ({(int)response.StatusCode}): {body}");

            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;

                if (!root.TryGetProperty("return", out var returnNode))
                    throw new InvalidOperationException($"پاسخ نامعتبر از کاوه‌نگار: {body}");

                var apiStatus = returnNode.TryGetProperty("status", out var statusNode)
                    ? statusNode.GetInt32()
                    : 0;
                var apiMessage = returnNode.TryGetProperty("message", out var messageNode)
                    ? messageNode.GetString()
                    : "خطای نامشخص کاوه‌نگار";

                if (apiStatus != 200)
                    throw new InvalidOperationException($"کاوه‌نگار: {apiMessage} (کد {apiStatus})");

                string? messageId = null;
                if (root.TryGetProperty("entries", out var entries))
                {
                    if (entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
                    {
                        var first = entries[0];
                        if (first.TryGetProperty("messageid", out var idNode))
                            messageId = idNode.ToString();
                    }
                    else if (entries.ValueKind == JsonValueKind.Object && entries.TryGetProperty("messageid", out var idNode))
                    {
                        messageId = idNode.ToString();
                    }
                }

                return (providerName, messageId);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"پاسخ JSON کاوه‌نگار قابل پردازش نیست: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiKey برای سرویس پیامک تنظیم نشده است.");
        if (string.IsNullOrWhiteSpace(configuredUrl))
            throw new InvalidOperationException("SendUrl برای سرویس پیامک تنظیم نشده است.");

        var genericClient = _httpClientFactory.CreateClient("SmsProvider");
        using var genericRequest = new HttpRequestMessage(HttpMethod.Post, configuredUrl);
        genericRequest.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        genericRequest.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            mobile,
            message,
            sender
        });

        using var genericResponse = await genericClient.SendAsync(genericRequest, ct);
        var genericBody = await genericResponse.Content.ReadAsStringAsync(ct);
        if (!genericResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"پاسخ پنل پیامک ناموفق بود: {(int)genericResponse.StatusCode} {genericBody}");

        string? providerMessageId = null;
        try
        {
            using var json = JsonDocument.Parse(genericBody);
            providerMessageId = json.RootElement.TryGetProperty("messageId", out var p)
                ? p.GetString()
                : json.RootElement.TryGetProperty("id", out var id) ? id.ToString() : null;
        }
        catch (JsonException)
        {
            // Some providers return plain text on success.
        }

        return (provider, providerMessageId);
    }

    private static string NormalizeMobile(string? mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.StartsWith("0098"))
            digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length == 12)
            digits = "0" + digits[2..];

        return digits;
    }

    private static bool IsValidMobile(string mobile)
        => mobile.Length == 11 && mobile.StartsWith("09") && mobile.All(char.IsDigit);
}
