using System.Text.Json;
using System.Text;
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

    public async Task<object> GetConfigurationStatusAsync()
    {
        var url = _configuration["Sms:SendUrl"]?.Trim();
        var apiKey = _configuration["Sms:ApiKey"]?.Trim();
        var provider = _configuration["Sms:Provider"]?.Trim();
        var sender = _configuration["Sms:Sender"]?.Trim();

        return new
        {
            configured = !string.IsNullOrWhiteSpace(apiKey),
            provider = string.IsNullOrWhiteSpace(provider) ? "HttpSmsProvider" : provider,
            hasSendUrl = !string.IsNullOrWhiteSpace(url),
            hasApiKey = !string.IsNullOrWhiteSpace(apiKey),
            hasSender = !string.IsNullOrWhiteSpace(sender)
        };
    }

    public async Task<object> SendAsync(SendSmsRequest request, CancellationToken ct = default)
    {
        var mobile = NormalizeMobile(request.Mobile);
        if (string.IsNullOrWhiteSpace(mobile)) throw new ArgumentException("شماره موبایل معتبر نیست.");
        if (string.IsNullOrWhiteSpace(request.Message)) throw new ArgumentException("متن پیامک خالی است.");

        int? templateId = request.TemplateId;
        if (templateId.HasValue && !await _context.SmsTemplates.AnyAsync(x => x.Id == templateId && x.IsActive, ct))
            throw new KeyNotFoundException("قالب پیامک یافت نشد یا غیرفعال است.");

        var message = request.Message.Trim();
        var providerName = _configuration["Sms:Provider"]?.Trim();

        var log = new SmsLog
        {
            PersonId = request.PersonId,
            Mobile = mobile,
            Message = message,
            TemplateId = templateId,
            Status = 1,
            Provider = string.IsNullOrWhiteSpace(providerName) ? "HttpSmsProvider" : providerName,
            CreatedAt = DateTime.UtcNow
        };
        _context.SmsLogs.Add(log);
        await _context.SaveChangesAsync(ct);

        try
        {
            var result = await SendToProviderAsync(mobile, message, ct);
            log.Status = 2;
            log.Provider = result.Provider;
            log.ProviderMessageId = result.ProviderMessageId;
            await _context.SaveChangesAsync(ct);
            return new
            {
                success = true,
                message = "پیامک با موفقیت ارسال شد.",
                providerMessageId = result.ProviderMessageId,
                provider = result.Provider
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
                message = "ارسال پیامک ناموفق بود.",
                providerMessageId = (string?)null,
                provider = log.Provider,
                error = log.ErrorMessage
            };
        }
    }

    // Kavenegar VerifyLookup sender for website order OTP.
    // Uses the API key already loaded by Program.cs from server.config.txt.
    public async Task<(bool Success, string Message, string? ProviderMessageId)> SendVerifyLookupAsync(
        string mobile,
        string token,
        string template,
        CancellationToken ct = default)
    {
        mobile = NormalizeMobile(mobile);
        token = (token ?? string.Empty).Trim();
        template = string.IsNullOrWhiteSpace(template) ? "VerifyLookup" : template.Trim();

        if (string.IsNullOrWhiteSpace(mobile))
            return (false, "شماره موبایل معتبر نیست.", null);
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
            return (false, "کد اعتبارسنجی معتبر نیست.", null);

        var apiKey = _configuration["Sms:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "کلید API کاوه‌نگار در server.config.txt تنظیم نشده است.", null);

        var url = $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/verify/lookup.json";
        var client = _httpClientFactory.CreateClient("SmsProvider");
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["receptor"] = mobile,
                ["token"] = token,
                ["template"] = template
            })
        };

        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        int? status = null;
        string? message = null;
        string? messageId = null;

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (root.TryGetProperty("return", out var ret))
            {
                if (ret.TryGetProperty("status", out var s) && s.TryGetInt32(out var parsed))
                    status = parsed;
                if (ret.TryGetProperty("message", out var m))
                    message = m.GetString();
            }

            if (root.TryGetProperty("entries", out var entries))
            {
                if (entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
                    messageId = ReadJsonString(entries[0], "messageid");
                else if (entries.ValueKind == JsonValueKind.Object)
                    messageId = ReadJsonString(entries, "messageid");
            }
        }
        catch (JsonException)
        {
            // Non-JSON response: HTTP status below determines success/failure.
        }

        if (!response.IsSuccessStatusCode || (status.HasValue && status.Value != 200))
        {
            var detail = message ?? $"کد پاسخ سرویس: {(status?.ToString() ?? ((int)response.StatusCode).ToString())}";
            return (false, $"ارسال کد تأیید ناموفق بود: {detail}", messageId);
        }

        return (true, "کد تأیید ارسال شد.", messageId);
    }

    private static string? ReadJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    public async Task<IReadOnlyList<object>> GetLogsAsync(int? personId = null, CancellationToken ct = default)
    {
        var query = _context.SmsLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt).AsQueryable();
        if (personId.HasValue) query = query.Where(x => x.PersonId == personId.Value).OrderByDescending(x => x.CreatedAt);
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
        if (activeOnly) query = query.Where(x => x.IsActive).OrderByDescending(x => x.Id);
        return await query.Select(x => new { x.Id, x.Name, x.TemplateText, x.IsActive, x.CreatedAt, x.UpdatedAt }).Cast<object>().ToListAsync(ct);
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

    private async Task<(string Provider, string? ProviderMessageId)> SendToProviderAsync(string mobile, string message, CancellationToken ct)
    {
        var configuredUrl = _configuration["Sms:SendUrl"]?.Trim();
        var apiKey = _configuration["Sms:ApiKey"]?.Trim();
        var sender = _configuration["Sms:Sender"]?.Trim();
        var provider = _configuration["Sms:Provider"]?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("کلید API سرویس پیامک تنظیم نشده است: Sms:ApiKey را در server.config.txt تنظیم کنید.");

        var url = string.IsNullOrWhiteSpace(configuredUrl)
            ? $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/sms/send.json"
            : configuredUrl.Replace("{API_KEY}", Uri.EscapeDataString(apiKey), StringComparison.OrdinalIgnoreCase);

        var client = _httpClientFactory.CreateClient("SmsProvider");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var form = new Dictionary<string, string>
        {
            ["receptor"] = mobile,
            ["message"] = message
        };
        if (!string.IsNullOrWhiteSpace(sender))
            form["sender"] = sender;

        request.Content = new FormUrlEncodedContent(form);
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var safeBody = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
            throw new InvalidOperationException($"پاسخ کاوه‌نگار ناموفق بود: {(int)response.StatusCode} {safeBody}");
        }

        string? providerMessageId = null;
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.TryGetProperty("return", out var ret))
            {
                if (ret.TryGetProperty("status", out var statusElement) && statusElement.TryGetInt32(out var status) && status < 200)
                    throw new InvalidOperationException($"کاوه‌نگار خطا برگرداند: {ret.GetProperty("message").GetString()}");

                if (ret.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
                {
                    var entry = entries[0];
                    providerMessageId = GetJsonString(entry, "messageid")
                        ?? GetJsonString(entry, "messageId")
                        ?? GetJsonString(entry, "id");
                }
            }
        }
        catch (JsonException)
        {
            // Keep compatibility with providers/proxies that return plain text.
        }

        return (string.IsNullOrWhiteSpace(provider) ? "Kavenegar" : provider, providerMessageId);
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static string NormalizeMobile(string mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0098")) digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length == 12) digits = "0" + digits[2..];

        if (digits.Length != 11 || !digits.StartsWith("09")) return string.Empty;
        return digits;
    }
}
