using System.Security.Cryptography;
using System.Text;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Auth;
using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Services.Implementations;

public sealed class MobileAuthService
{
    private readonly KianStoreDbContext _db;

    public MobileAuthService(KianStoreDbContext db)
    {
        _db = db;
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

        return new LoginResponse
        {
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
}
