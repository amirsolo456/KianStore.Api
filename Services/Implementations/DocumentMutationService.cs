using System.Data;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KianStore.Api.Services.Implementations;

public sealed class DocumentMutationService : IDocumentMutationService
{
    private const int PartnerSaleType = 113;
    private static readonly SemaphoreSlim AuditSchemaLock = new(1, 1);
    private readonly KianStoreDbContext _context;
    private readonly IDocumentService _documentService;

    public DocumentMutationService(KianStoreDbContext context, IDocumentService documentService)
    {
        _context = context;
        _documentService = documentService;
    }

    public async Task<ApiResponse<DocumentResponse>> UpdatePartnerSaleAsync(
        int idSal,
        string id,
        CreateDocumentRequest request,
        int? currentUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var sanad = await _context.Sanads.FirstOrDefaultAsync(
            x => x.IdSal == idSal && x.Id == id && x.SanadType == PartnerSaleType && !x.Disable,
            cancellationToken);

        if (sanad == null)
            throw new ApiException(404, "PARTNER_SALE_NOT_FOUND", "سند فروش از انبار همکار یافت نشد.");

        request = CloneAsPartnerSale(request, idSal);
        await ValidateReferencesAsync(request, cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var products = await LoadProductsAsync(request, cancellationToken);
            ApplyHeader(sanad, request, request.IdFaktor ?? sanad.IdFaktor);

            _context.SanadDetails.RemoveRange(_context.SanadDetails.Where(x => x.IdSal == idSal && x.IdSanad == id));

            var details = BuildDetails(request, id, products);
            var total = details.Sum(x => x.SumMab);
            sanad.MabKol = total;
            sanad.MabFrosh = total;
            sanad.MabBed = total;
            sanad.IsFinal = true;
            sanad.IsSavedFinal = true;
            sanad.Disable = false;

            _context.SanadDetails.AddRange(details);
            await _context.SaveChangesAsync(cancellationToken);

            var user = await ResolveUserAsync(currentUserId ?? request.IdMasool, cancellationToken);
            await EnsureAuditSchemaAsync(cancellationToken);
            await InsertAuditAsync(idSal, id, PartnerSaleType, "UPDATE", user, "ویرایش سند فروش از انبار همکار", cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return await LoadResponseAsync(idSal, id, "سند فروش از انبار همکار با موفقیت ویرایش شد.", cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<DocumentResponse>> DeletePartnerSaleAsync(
        int idSal,
        string id,
        int? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var sanad = await _context.Sanads.FirstOrDefaultAsync(
            x => x.IdSal == idSal && x.Id == id && x.SanadType == PartnerSaleType && !x.Disable,
            cancellationToken);

        if (sanad == null)
            throw new ApiException(404, "PARTNER_SALE_NOT_FOUND", "سند فروش از انبار همکار یافت نشد.");

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            sanad.Disable = true;
            sanad.IsFinal = false;
            sanad.IsSavedFinal = false;
            sanad.ShowInSanad = false;
            sanad.ShowInFaktor = false;

            await _context.SaveChangesAsync(cancellationToken);

            var user = await ResolveUserAsync(currentUserId ?? sanad.IdMasool, cancellationToken);
            await EnsureAuditSchemaAsync(cancellationToken);
            await InsertAuditAsync(idSal, id, PartnerSaleType, "DELETE", user, "حذف سند فروش از انبار همکار", cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return await LoadResponseAsync(idSal, id, "سند فروش از انبار همکار حذف شد.", cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void ValidateRequest(CreateDocumentRequest request)
    {
        if (request.Items.Count == 0)
            throw new ApiException(400, "EMPTY_DOCUMENT", "سند حداقل باید یک قلم داشته باشد.");
        if (request.SabtDate.Length != 10)
            throw new ApiException(400, "INVALID_DATE", "تاریخ سند باید به صورت yyyy/MM/dd باشد.");
        if (request.IdSal <= 0)
            throw new ApiException(400, "INVALID_YEAR", "سال مالی سند معتبر نیست.");
    }

    private async Task ValidateReferencesAsync(CreateDocumentRequest request, CancellationToken ct)
    {
        var tarafExists = await _context.Tarafs.AsNoTracking().AnyAsync(
            x => x.Id == request.IdTaraf && x.IdType == request.IdTarafType && !x.IsDisabled, ct);
        if (!tarafExists)
            throw new ApiException(404, "CUSTOMER_NOT_FOUND", "طرف حساب مورد نظر یافت نشد.");

        var cashboxExists = await _context.CheckDefs.AsNoTracking().AnyAsync(
            x => x.Id == request.IdSandogh && x.Type == request.IdSandoghType, ct);
        if (!cashboxExists)
            throw new ApiException(409, "INVALID_CASHBOX", "صندوق/حساب انتخاب‌شده معتبر نیست.");
    }

    private async Task<Dictionary<string, Kala>> LoadProductsAsync(CreateDocumentRequest request, CancellationToken ct)
    {
        var ids = request.Items.Select(x => x.IdKala.Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal).ToList();
        var products = await _context.Kalas.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ApiException(400, "INVALID_QUANTITY", $"تعداد کالای {item.IdKala} باید بیشتر از صفر باشد.");
            if (!products.TryGetValue(item.IdKala, out var product))
                throw new ApiException(404, "PRODUCT_NOT_FOUND", $"کالا با کد {item.IdKala} یافت نشد.");
            if (product.IsDisabled)
                throw new ApiException(409, "PRODUCT_DISABLED", $"کالای {item.IdKala} غیرفعال است.");
        }
        return products;
    }

    private static CreateDocumentRequest CloneAsPartnerSale(CreateDocumentRequest request, int idSal)
        => new()
        {
            IdSal = idSal,
            SanadType = PartnerSaleType,
            IdAnbar = request.IdAnbar,
            IdTaraf = request.IdTaraf,
            IdTarafType = request.IdTarafType,
            IdMasool = request.IdMasool,
            IdFaktor = request.IdFaktor,
            IdSandogh = request.IdSandogh,
            IdSandoghType = request.IdSandoghType,
            SabtDate = request.SabtDate,
            Des = request.Des,
            Sharh = request.Sharh,
            CheckStock = request.CheckStock,
            IsPending = false,
            SefareshID = request.SefareshID,
            DiscountCodes = request.DiscountCodes,
            NextPurchaseDiscount = request.NextPurchaseDiscount,
            Items = request.Items
        };

    private static void ApplyHeader(Sanad sanad, CreateDocumentRequest request, int factorId)
    {
        sanad.SanadType = PartnerSaleType;
        sanad.IdAnbar = request.IdAnbar;
        sanad.IdTaraf = request.IdTaraf;
        sanad.IdTarafType = request.IdTarafType;
        sanad.IdFaktor = factorId;
        sanad.IdMasool = request.IdMasool;
        sanad.SabtDate = request.SabtDate;
        sanad.TasviehDate = request.SabtDate;
        sanad.Des = request.Des;
        sanad.Sharh = request.Sharh;
        sanad.IDSandogh = request.IdSandogh;
        sanad.IDSandoghType = request.IdSandoghType;
        sanad.IDAnbar2 = request.IdAnbar;
        sanad.IDTaraf2 = request.IdTaraf;
        sanad.IDTarafTahator = request.IdTaraf;
        sanad.SefareshID = request.SefareshID;
    }

    private static List<SanadDetail> BuildDetails(CreateDocumentRequest request, string sanadId, Dictionary<string, Kala> products)
    {
        var details = new List<SanadDetail>(request.Items.Count);
        var row = 1;
        foreach (var item in request.Items)
        {
            var product = products[item.IdKala];
            var unitPrice = item.UnitPrice ?? product.MabFrosh;
            var purchasePrice = item.PurchasePrice ?? product.MabKharid;
            var total = unitPrice * item.Quantity;
            details.Add(new SanadDetail
            {
                IdSal = request.IdSal,
                IdSanad = sanadId,
                Id2 = row++,
                IdKala = product.Id,
                Bed = 0,
                Bes = (double)item.Quantity,
                BedMab = 0,
                BesMab = unitPrice,
                Des = item.Description,
                SumMab = total,
                IdAnbar = request.IdAnbar,
                IdKalaType = product.KalaType,
                BedMabKharid = purchasePrice,
                Maliat = 0,
                Maliat1 = false,
                Maliat2 = false,
                TakhfifDarsad = 0,
                PorsantDarsad = 0,
                HazKala = 0,
                HazKalaKharid = 0,
                IdSanjesh = product.IdSanjesh,
                IdSanjesh2 = product.IdSanjesh2,
                BedBesZarib = 1,
                SanadType = PartnerSaleType,
                IdAttribValuesStock = string.Empty,
                SumTakhfifKala = 0,
                Bed2 = 0,
                Bes2 = (double)item.Quantity,
                BedMab2 = 0,
                BesMab2 = unitPrice
            });
        }
        return details;
    }

    private async Task<ApiResponse<DocumentResponse>> LoadResponseAsync(int idSal, string id, string message, CancellationToken ct)
    {
        var result = await _documentService.GetAsync(idSal, id, ct);
        if (!result.Success || result.Data == null)
            return ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_RESPONSE_LOAD_FAILED", "اطلاعات نهایی سند قابل بازیابی نبود.");
        return ApiResponse<DocumentResponse>.SuccessResult(result.Data, message);
    }

    private sealed record AuditUser(int? Id, string? UserName, string? FullName);

    private async Task<AuditUser> ResolveUserAsync(int? userId, CancellationToken ct)
    {
        if (userId is null || userId <= 0)
            return new AuditUser(null, null, null);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TOP (1) ID, UserName, UserFLName FROM dbo.Users WHERE ID = @Id";
            command.CommandType = CommandType.Text;
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var p = command.CreateParameter();
            p.ParameterName = "@Id";
            p.DbType = DbType.Int32;
            p.Value = userId.Value;
            command.Parameters.Add(p);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return new AuditUser(userId, null, null);
            return new AuditUser(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2));
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private async Task EnsureAuditSchemaAsync(CancellationToken ct)
    {
        await AuditSchemaLock.WaitAsync(ct);
        try
        {
            const string sql = @"
IF OBJECT_ID(N'dbo.SanadChangeLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SanadChangeLog
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SanadChangeLog PRIMARY KEY,
        IdSal INT NOT NULL,
        IdSanad VARCHAR(20) NOT NULL,
        SanadType INT NOT NULL,
        [Action] VARCHAR(20) NOT NULL,
        UserId INT NULL,
        UserName NVARCHAR(100) NULL,
        UserFullName NVARCHAR(150) NULL,
        ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_SanadChangeLog_ChangedAt DEFAULT SYSUTCDATETIME(),
        [Description] NVARCHAR(500) NULL
    );
    CREATE INDEX IX_SanadChangeLog_Document ON dbo.SanadChangeLog(IdSal, IdSanad, ChangedAt DESC);
    CREATE INDEX IX_SanadChangeLog_User ON dbo.SanadChangeLog(UserId, ChangedAt DESC);
END";
            await _context.Database.ExecuteSqlRawAsync(sql, ct);
        }
        finally
        {
            AuditSchemaLock.Release();
        }
    }

    private async Task InsertAuditAsync(int idSal, string id, int sanadType, string action, AuditUser user, string description, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO dbo.SanadChangeLog
    (IdSal, IdSanad, SanadType, [Action], UserId, UserName, UserFullName, ChangedAt, [Description])
VALUES
    (@idSal, @idSanad, @sanadType, @action, @userId, @userName, @userFullName, SYSUTCDATETIME(), @description);";
        var parameters = new[]
        {
            new SqlParameter("@idSal", idSal),
            new SqlParameter("@idSanad", id),
            new SqlParameter("@sanadType", sanadType),
            new SqlParameter("@action", action),
            new SqlParameter("@userId", (object?)user.Id ?? DBNull.Value),
            new SqlParameter("@userName", (object?)user.UserName ?? DBNull.Value),
            new SqlParameter("@userFullName", (object?)user.FullName ?? DBNull.Value),
            new SqlParameter("@description", description)
        };
        await _context.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }
}
