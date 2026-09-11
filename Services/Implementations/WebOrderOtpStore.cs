using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace KianStore.Api.Services.Implementations;

public static class WebOrderOtpStore
{
    private sealed record Entry(string Mobile, string Code, DateTimeOffset ExpiresAt, DateTimeOffset NextSendAt, bool Verified);
    private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    public static async Task<(bool Success, string Message, string? Challenge, int ExpiresInSeconds)> SendAsync(
        string mobile, IConfiguration configuration, IHttpClientFactory httpFactory, CancellationToken ct)
    {
        mobile = NormalizeMobile(mobile);
        if (string.IsNullOrWhiteSpace(mobile)) return (false, "شماره موبایل معتبر نیست.", null, 0);

        var now = DateTimeOffset.UtcNow;
        var old = Entries.FirstOrDefault(x => x.Value.Mobile == mobile && !x.Value.Verified);
        if (old.Value is not null && now < old.Value.NextSendAt)
        {
            var wait = Math.Max(1, (int)Math.Ceiling((old.Value.NextSendAt - now).TotalSeconds));
            return (false, $"لطفاً {wait} ثانیه دیگر دوباره درخواست کد کنید.", null, wait);
        }

        var apiKey = configuration["Sms:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey)) return (false, "کلید API کاوه‌نگار تنظیم نشده است.", null, 0);
        var template = configuration["WebOrderOtpTemplate"]?.Trim();
        if (string.IsNullOrWhiteSpace(template)) template = "Verify/Lookup";

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var ttl = GetInt(configuration, "WebOrderOtpTtlSeconds", 300);
        var resend = GetInt(configuration, "WebOrderOtpResendSeconds", 60);
        var url = $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/verify/lookup.json";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["receptor"] = mobile,
                ["token"] = code,
                ["template"] = template,
                ["type"] = "sms"
            })
        };

        try
        {
            using var response = await httpFactory.CreateClient("SmsProvider").SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) return (false, $"ارسال کد تأیید ناموفق بود. HTTP {(int)response.StatusCode}.", null, 0);

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.TryGetProperty("return", out var ret) && ret.ValueKind == JsonValueKind.Object &&
                ret.TryGetProperty("status", out var status) && status.TryGetInt32(out var value) && value < 200)
            {
                var msg = ret.TryGetProperty("message", out var m) ? m.GetString() : null;
                return (false, msg ?? "کاوه‌نگار ارسال کد را رد کرد.", null, 0);
            }

            var challenge = Guid.NewGuid().ToString("N");
            Entries[challenge] = new Entry(mobile, code, now.AddSeconds(ttl), now.AddSeconds(resend), false);
            foreach (var pair in Entries)
                if (pair.Key != challenge && pair.Value.Mobile == mobile) Entries.TryRemove(pair.Key, out _);

            return (true, "کد تأیید ارسال شد.", challenge, ttl);
        }
        catch (Exception ex)
        {
            return (false, $"ارسال کد تأیید با خطا مواجه شد: {ex.Message}", null, 0);
        }
    }

    public static (bool Success, string Message) Verify(string mobile, string code, string challenge)
    {
        mobile = NormalizeMobile(mobile);
        if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(challenge))
            return (false, "اطلاعات کد تأیید معتبر نیست.");
        if (!Entries.TryGetValue(challenge, out var entry)) return (false, "کد تأیید منقضی شده یا معتبر نیست.");
        if (!string.Equals(entry.Mobile, mobile, StringComparison.Ordinal)) return (false, "شماره موبایل با کد تأیید مطابقت ندارد.");
        if (DateTimeOffset.UtcNow > entry.ExpiresAt) { Entries.TryRemove(challenge, out _); return (false, "کد تأیید منقضی شده است."); }
        var ok = code.Trim().Length == 6 && CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(entry.Code), System.Text.Encoding.UTF8.GetBytes(code.Trim()));
        if (!ok) return (false, "کد تأیید اشتباه است.");
        Entries[challenge] = entry with { Verified = true };
        return (true, "شماره موبایل با موفقیت تأیید شد.");
    }

    public static bool IsVerified(string mobile, string challenge)
    {
        mobile = NormalizeMobile(mobile);
        if (!Entries.TryGetValue(challenge ?? string.Empty, out var entry)) return false;
        if (!entry.Verified || !string.Equals(entry.Mobile, mobile, StringComparison.Ordinal)) return false;
        if (DateTimeOffset.UtcNow > entry.ExpiresAt) { Entries.TryRemove(challenge, out _); return false; }
        return true;
    }

    private static int GetInt(IConfiguration configuration, string key, int fallback)
        => int.TryParse(configuration[key], out var value) ? value : fallback;

    public static string NormalizeMobile(string mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0098")) digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length == 12) digits = "0" + digits[2..];
        return digits.Length == 11 && digits.StartsWith("09") ? digits : string.Empty;
    }
}
