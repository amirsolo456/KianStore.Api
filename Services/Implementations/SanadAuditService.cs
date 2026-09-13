using System.Data;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Services.Implementations;

public sealed class SanadAuditService : ISanadAuditService
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private readonly KianStoreDbContext _context;

    public SanadAuditService(KianStoreDbContext context) => _context = context;

    public async Task RecordAsync(DocumentResponse document, int? currentUserId, string action, string description, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var userId = currentUserId.GetValueOrDefault() > 0 ? currentUserId : null;
        string? userName = null;
        string? fullName = null;
        if (userId.HasValue)
        {
            var connection = _context.Database.GetDbConnection();
            var close = connection.State != ConnectionState.Open;
            if (close) await connection.OpenAsync(ct);
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT TOP (1) UserName, UserFLName FROM dbo.Users WHERE ID=@id";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.DbType = DbType.Int32; p.Value = userId.Value; cmd.Parameters.Add(p);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct)) { userName = reader.IsDBNull(0) ? null : reader.GetString(0); fullName = reader.IsDBNull(1) ? null : reader.GetString(1); }
            }
            finally { if (close) await connection.CloseAsync(); }
        }
        const string sql = "INSERT INTO dbo.SanadChangeLog(IdSal,IdSanad,SanadType,[Action],UserId,UserName,UserFullName,ChangedAt,[Description]) VALUES(@s,@i,@t,@a,@u,@n,@f,SYSUTCDATETIME(),@d)";
        await _context.Database.ExecuteSqlRawAsync(sql,
            new SqlParameter("@s", document.IdSal), new SqlParameter("@i", document.Id), new SqlParameter("@t", document.SanadType),
            new SqlParameter("@a", action), new SqlParameter("@u", (object?)userId ?? DBNull.Value), new SqlParameter("@n", (object?)userName ?? DBNull.Value),
            new SqlParameter("@f", (object?)fullName ?? DBNull.Value), new SqlParameter("@d", description), ct);
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await SchemaLock.WaitAsync(ct);
        try
        {
            const string sql = @"
IF OBJECT_ID(N'dbo.SanadChangeLog',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.SanadChangeLog(
  Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SanadChangeLog PRIMARY KEY,
  IdSal INT NOT NULL, IdSanad VARCHAR(20) NOT NULL, SanadType INT NOT NULL,
  [Action] VARCHAR(20) NOT NULL, UserId INT NULL, UserName NVARCHAR(100) NULL,
  UserFullName NVARCHAR(150) NULL, ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_SanadChangeLog_ChangedAt DEFAULT SYSUTCDATETIME(),
  [Description] NVARCHAR(500) NULL);
 CREATE INDEX IX_SanadChangeLog_Document ON dbo.SanadChangeLog(IdSal,IdSanad,ChangedAt DESC);
 CREATE INDEX IX_SanadChangeLog_User ON dbo.SanadChangeLog(UserId,ChangedAt DESC);
END";
            await _context.Database.ExecuteSqlRawAsync(sql, ct);
        }
        finally { SchemaLock.Release(); }
    }
}
