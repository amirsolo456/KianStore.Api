using System.Text.Json;

namespace KianStore.Api.Services.Implementations;

public static class SmsServiceVerifyLookupExtensions
{
    public static async Task<(bool Success, string Message, string? ProviderMessageId)> SendVerifyLookupAsync(
        this SmsService smsService,
        string mobile,
        string token,
        string template,
        CancellationToken ct = default)
    {
        // This extension intentionally uses the existing SmsService configuration and HTTP client.
        // The API key is read from server.config.txt by Program.cs; it is never stored in source control.
        var configuration = smsService.GetType()
            .GetField("_configuration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(smsService) as IConfiguration;
        var factory = smsService.GetType()
            .GetField("_httpClientFactory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(smsService) as IHttpClientFactory;

        if (configuration is null || factory is null)
            return (false, "تنظیمات سرویس پیامک قابل دسترسی نیست.", null);

        var apiKey = configuration["Sms:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "کلید API کاوه‌نگار در server.config.txt تنظیم نشده است.", null);

        mobile = NormalizeMobile(mobile);
        token = (token ?? string.Empty).Trim();
        template = string.IsNullOrWhiteSpace(template) ? "VerifyLookup" : template.Trim();

        if (string.IsNullOrWhiteSpace(mobile))
            return (false, "شماره موبایل معتبر نیست.", null);
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
            return (false, "کد اعتبارسنجی معتبر نیست.", null);

        var url = $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/verify/lookup.json";
        var client = factory.CreateClient("SmsProvider");
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
                if (ret.TryGetProperty("status", out var s) && s.TryGetInt32(out var parsed)) status = parsed;
                if (ret.TryGetProperty("message", out var m)) message = m.GetString();
            }
            if (root.TryGetProperty("entries", out var entries))
            {
                if (entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
                    messageId = ReadString(entries[0], "messageid");
                else if (entries.ValueKind == JsonValueKind.Object)
                    messageId = ReadString(entries, "messageid");
            }
        }
        catch (JsonException)
        {
            // handled below using HTTP status
        }

        if (!response.IsSuccessStatusCode || status is not null && status != 200)
        {
            var detail = message ?? $"کد پاسخ سرویس: {(status?.ToString() ?? ((int)response.StatusCode).ToString())}";
            return (false, $"ارسال کد تأیید ناموفق بود: {detail}", messageId);
        }

        return (true, "کد تأیید ارسال شد.", messageId);
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
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
        return digits.Length == 11 && digits.StartsWith("09") ? digits : string.Empty;
    }
}
