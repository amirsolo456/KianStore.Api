using System.Net.Http.Json;
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

    public async Task<object> GetConfigurationStatusAsync()
    {
        var url = _configuration["Sms:SendUrl"]?.Trim();
        var apiKey = _configuration["Sms:ApiKey"]?.Trim();
        var provider = _configuration["Sms:Provider"]?.Trim();
        var sender = _configuration["Sms:Sender"]?.Trim();

        return new
        {
            configured = !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(apiKey),
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
        var url = _configuration["Sms:SendUrl"]?.Trim();
        var apiKey = _configuration["Sms:ApiKey"]?.Trim();
        var sender = _configuration["Sms:Sender"]?.Trim();
        var provider = _configuration["Sms:Provider"]?.Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("تنظیمات سرویس پیامک کامل نشده است: Sms:SendUrl و Sms:ApiKey را تنظیم کنید.");

        var client = _httpClientFactory.CreateClient("SmsProvider");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mobile,
            message,
            sender
        });

        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var safeBody = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
            throw new InvalidOperationException($"پاسخ پنل پیامک ناموفق بود: {(int)response.StatusCode} {safeBody}");
        }

        string? providerMessageId = null;
        try
        {
            using var json = JsonDocument.Parse(body);
            providerMessageId = TryGetString(json.RootElement, "messageId")
                ?? TryGetString(json.RootElement, "message_id")
                ?? TryGetString(json.RootElement, "id")
                ?? TryGetString(json.RootElement, "resultId")
                ?? TryGetString(json.RootElement, "result_id");
        }
        catch (JsonException)
        {
            // Some providers return plain text on success; provider id is optional.
        }

        return (string.IsNullOrWhiteSpace(provider) ? "HttpSmsProvider" : provider, providerMessageId);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
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
