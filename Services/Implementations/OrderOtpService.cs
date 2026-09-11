using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace KianStore.Api.Services.Implementations;

public sealed class OrderOtpService
{
    private sealed record Challenge(string Mobile, string Code, DateTimeOffset ExpiresAt, DateTimeOffset NextSendAt, bool Verified);
    private readonly ConcurrentDictionary<string, Challenge> _challenges = new(StringComparer.Ordinal);
    private readonly SmsService _smsService;
    private readonly IConfiguration _configuration;

    public OrderOtpService(SmsService smsService, IConfiguration configuration)
    {
        _smsService = smsService;
        _configuration = configuration;
    }

    public async Task<(bool Success, string Message, string? Challenge, int ExpiresInSeconds)> SendAsync(string mobile, CancellationToken ct = default)
    {
        mobile = NormalizeMobile(mobile);
        if (string.IsNullOrWhiteSpace(mobile)) return (false, "شماره موبایل معتبر نیست.", null, 0);
        var now = DateTimeOffset.UtcNow;
        var current = _challenges.FirstOrDefault(x => x.Value.Mobile == mobile && !x.Value.Verified);
        if (current.Value is not null && now < current.Value.NextSendAt)
        {
            var wait = Math.Max(1, (int)Math.Ceiling((current.Value.NextSendAt - now).TotalSeconds));
            return (false, $"لطفاً {wait} ثانیه دیگر دوباره درخواست کد کنید.", null, wait);
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var ttl = GetInt("WebOrderOtpTtlSeconds", 300);
        var resend = GetInt("WebOrderOtpResendSeconds", 60);
        var template = _configuration["WebOrderOtpTemplate"]?.Trim();
        if (string.IsNullOrWhiteSpace(template)) template = "Verify/Lookup";

        var send = await _smsService.SendVerifyLookupAsync(mobile, code, template, ct);
        if (!send.Success) return (false, send.Message, null, 0);

        var challenge = Guid.NewGuid().ToString("N");
        _challenges[challenge] = new Challenge(mobile, code, now.AddSeconds(ttl), now.AddSeconds(resend), false);
        foreach (var pair in _challenges)
            if (pair.Key != challenge && pair.Value.Mobile == mobile)
                _challenges.TryRemove(pair.Key, out _);

        return (true, "کد تأیید ارسال شد.", challenge, ttl);
    }

    public (bool Success, string Message) Verify(string mobile, string code, string challenge)
    {
        mobile = NormalizeMobile(mobile);
        code = (code ?? string.Empty).Trim();
        challenge = (challenge ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mobile) || code.Length != 6 || string.IsNullOrWhiteSpace(challenge))
            return (false, "اطلاعات کد تأیید معتبر نیست.");
        if (!_challenges.TryGetValue(challenge, out var item))
            return (false, "کد تأیید منقضی شده یا معتبر نیست. دوباره درخواست کد کنید.");
        if (!string.Equals(item.Mobile, mobile, StringComparison.Ordinal))
            return (false, "شماره موبایل با کد تأیید مطابقت ندارد.");
        if (DateTimeOffset.UtcNow > item.ExpiresAt)
        {
            _challenges.TryRemove(challenge, out _);
            return (false, "کد تأیید منقضی شده است. دوباره درخواست کد کنید.");
        }
        if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(item.Code), System.Text.Encoding.UTF8.GetBytes(code)))
            return (false, "کد تأیید اشتباه است.");
        _challenges[challenge] = item with { Verified = true };
        return (true, "شماره موبایل با موفقیت تأیید شد.");
    }

    public bool IsVerified(string mobile, string challenge)
    {
        mobile = NormalizeMobile(mobile);
        if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(challenge)) return false;
        if (!_challenges.TryGetValue(challenge, out var item)) return false;
        if (!item.Verified || !string.Equals(item.Mobile, mobile, StringComparison.Ordinal)) return false;
        if (DateTimeOffset.UtcNow > item.ExpiresAt)
        {
            _challenges.TryRemove(challenge, out _);
            return false;
        }
        return true;
    }

    private int GetInt(string key, int fallback) => int.TryParse(_configuration[key], out var value) ? value : fallback;

    private static string NormalizeMobile(string mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0098")) digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length == 12) digits = "0" + digits[2..];
        return digits.Length == 11 && digits.StartsWith("09") ? digits : string.Empty;
    }
}
