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
        var configuration = smsService.GetType().GetField("_configuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(smsService) as IConfiguration;
        var httpFactory = smsService.GetType().GetField("_httpClientFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(smsService) as IHttpClientFactory;
        if (configuration is null || httpFactory is null)
            return (false, "پیکربندی سرویس پیامک در دسترس نیست.", null);

        var apiKey = configuration["Sms:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "کلید API کاوه‌نگار تنظیم نشده است.", null);

        var url = $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/verify/lookup.json";
        var client = httpFactory.CreateClient("SmsProvider");
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["receptor"] = mobile,
                ["token"] = token,
                ["template"] = template,
                ["type"] = "sms"
            })
        };

        try
        {
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return (false, $"ارسال کد تأیید ناموفق بود. HTTP {(int)response.StatusCode}.", null);

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.TryGetProperty("return", out var ret) && ret.ValueKind == JsonValueKind.Object &&
                ret.TryGetProperty("status", out var status) && status.TryGetInt32(out var statusValue) && statusValue < 200)
            {
                var message = ret.TryGetProperty("message", out var m) ? m.GetString() : null;
                return (false, $"کاوه‌نگار: {message ?? "خطا در ارسال کد تأیید."}", null);
            }

            string? messageId = null;
            if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
            {
                var entry = entries[0];
                if (entry.TryGetProperty("messageid", out var id)) messageId = id.ToString();
            }
            return (true, "کد تأیید ارسال شد.", messageId);
        }
        catch (Exception ex)
        {
            return (false, $"ارسال کد تأیید با خطا مواجه شد: {ex.Message}", null);
        }
    }
}
