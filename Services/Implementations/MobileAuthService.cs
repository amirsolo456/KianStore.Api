using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Auth;
using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace KianStore.Api.Services.Implementations;

public sealed class MobileAuthService
{
    private readonly KianStoreDbContext _db;
    private readonly IConfiguration _configuration;

    public MobileAuthService(KianStoreDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        var user = await _db.Set<Users>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserName == username, ct);

        if (user == null || !VerifyPassword(password, user.Pass))
            return null;

        var expiresAt = DateTime.UtcNow.AddDays(GetTokenLifetimeDays());
        var keyText = _configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("Jwt__Key");
        if (string.IsNullOrWhiteSpace(keyText) || Encoding.UTF8.GetByteCount(keyText) < 32)
            throw new InvalidOperationException("JWT signing key is missing or too short. Configure Jwt:Key or Jwt__Key with at least 32 bytes.");

        var issuer = _configuration["Jwt:Issuer"] ?? "KianStore.Api";
        var audience = _configuration["Jwt:Audience"] ?? "KianStore.Mobile";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim("full_name", user.UserFLName ?? string.Empty),
            new Claim("access", user.Access.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new LoginResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAt,
            UserId = user.Id,
            UserName = user.UserName,
            FullName = user.UserFLName ?? string.Empty,
            Access = user.Access
        };
    }

    private static bool VerifyPassword(string password, string storedPassword)
    {
        if (string.IsNullOrEmpty(storedPassword)) return false;

        // The legacy KianStore Users table stores the current credential in Pass.
        // Keep comparison constant-time. Existing credentials remain compatible.
        var provided = Encoding.UTF8.GetBytes(password);
        var stored = Encoding.UTF8.GetBytes(storedPassword);
        return provided.Length == stored.Length && CryptographicOperations.FixedTimeEquals(provided, stored);
    }

    private int GetTokenLifetimeDays()
        => int.TryParse(_configuration["Jwt:TokenLifetimeDays"], out var days) && days > 0 ? days : 30;
}
