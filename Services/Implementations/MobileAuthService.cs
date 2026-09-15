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

    public async Task<bool> CreateUserAsync(CreateMobileUserRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var password = request.Password;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;
        if (username.Length > 20 || password.Length > 100)
            return false;

        var exists = await _db.Set<Users>().AnyAsync(x => x.UserName == username, ct);
        if (exists)
            throw new InvalidOperationException("این نام کاربری قبلاً ثبت شده است.");

        var firstName = (request.FirstName ?? string.Empty).Trim();
        var lastName = (request.LastName ?? string.Empty).Trim();
        var post = string.IsNullOrWhiteSpace(request.Post) ? "کاربر موبایل" : request.Post.Trim();
        var access = request.Access > 0 ? request.Access : 2;

        var sql = @"
DECLARE @NewId INT = ISNULL((SELECT MAX(ID) FROM dbo.Users), 0) + 1;
INSERT INTO dbo.Users
    (ID,LName,FName,Post,UserName,Pass,[Access],ZaribKarmozdFrosh,IDSandogh,IDSandoghType,
     LockByIDSandogh,IDAnbar,LockByIDAnbar,PassAnd,IDAnbar1,IDAnbar2,IDHyperMarket,IDGroup)
VALUES
    (@NewId,@LastName,@FirstName,@Post,@UserName,@Password,@Access,0,0,1,
     0,0,0,'',0,-1,0,'');";

        await _db.Database.ExecuteSqlRawAsync(sql,
            new Microsoft.Data.SqlClient.SqlParameter("@LastName", lastName),
            new Microsoft.Data.SqlClient.SqlParameter("@FirstName", firstName),
            new Microsoft.Data.SqlClient.SqlParameter("@Post", post),
            new Microsoft.Data.SqlClient.SqlParameter("@UserName", username),
            new Microsoft.Data.SqlClient.SqlParameter("@Password", password),
            new Microsoft.Data.SqlClient.SqlParameter("@Access", access), ct);

        return true;
    }

    private static bool VerifyPassword(string password, string storedPassword)
    {
        if (string.IsNullOrEmpty(storedPassword)) return false;
        var provided = Encoding.UTF8.GetBytes(password);
        var stored = Encoding.UTF8.GetBytes(storedPassword);
        return provided.Length == stored.Length && CryptographicOperations.FixedTimeEquals(provided, stored);
    }

    private int GetTokenLifetimeDays()
        => int.TryParse(_configuration["Jwt:TokenLifetimeDays"], out var days) && days > 0 ? days : 30;
}
