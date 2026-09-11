using System.Data;
using System.Data.Common;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KianStore.Api.Services.Implementations;

/// <summary>
/// Routes pending website orders through the existing KianStore Sanad/SanadDetail
/// procedures without changing the existing legacy document behavior.
/// </summary>
public sealed class PendingAwareDocumentService : IDocumentService
{
    private readonly KianStoreDbContext _context;
    private readonly LegacyDocumentService _legacy;

    public PendingAwareDocumentService(KianStoreDbContext context, LegacyDocumentService legacy)
    {
        _context = context;
        _legacy = legacy;
    }

    public Task<ApiResponse<DocumentResponse>> CreateAsync(
        CreateDocumentRequest request,
        CancellationToken cancellationToken = default)
        => request.IsPending
            ? CreatePendingAsync(request, cancellationToken)
            : _legacy.CreateAsync(request, cancellationToken);

    public Task<ApiResponse<DocumentResponse>> GetAsync(
        int idSal,
        string id,
        CancellationToken cancellationToken = default)
        => _legacy.GetAsync(idSal, id, cancellationToken);

    public Task<ApiResponse<IReadOnlyList<DocumentResponse>>> GetHistoryAsync(
        int idSal,
        int sanadType = 12,
        int page = 1,
        int pageSize = 30,
        CancellationToken cancellationToken = default)
        => _legacy.GetHistoryAsync(idSal, sanadType, page, pageSize, cancellationToken);

    private async Task<ApiResponse<DocumentResponse>> CreatePendingAsync(
        CreateDocumentRequest request,
        CancellationToken ct)
    {
        ValidatePendingRequest(request);

        var tarafExists = await _context.Tarafs.AsNoTracking().AnyAsync(
            x => x.Id == request.IdTaraf &&
                 x.IdType == request.IdTarafType &&
                 !x.IsDisabled,
            ct);

        if (!tarafExists)
            throw new ApiException(404, "CUSTOMER_NOT_FOUND", "طرف حساب مورد نظر یافت نشد.");

        var kalaIds = request.Items
            .Select(x => x.IdKala.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var productsList = await _context.Kalas.AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id))
            .ToListAsync(ct);

        var products = productsList.ToDictionary(
            x => x.Id,
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ApiException(400, "INVALID_QUANTITY", $"تعداد کالای {item.IdKala} باید بیشتر از صفر باشد.");

            if (!products.TryGetValue(item.IdKala.Trim(), out var product))
                throw new ApiException(404, "PRODUCT_NOT_FOUND", $"کالا با کد {item.IdKala} یافت نشد.");

            if (product.IsDisabled)
                throw new ApiException(409, "PRODUCT_DISABLED", $"کالای {item.IdKala} غیرفعال است.");
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            var header = await CreateHeaderAsync(request, transaction, ct);

            await ExecuteSqlAsync(
                "UPDATE dbo.Sanad SET IDAnbar=@idAnbar, IDAnbar2=@idAnbar2, IDSandogh=@idSandogh, IDSandoghType=@idSandoghType, IsFinal=0, IsSavedFinal=0, SefareshID=@sefareshID WHERE IDSal=@idSal AND ID=@id",
                transaction,
                ct,
                new SqlParameter("@idAnbar", SqlDbType.Int) { Value = request.IdAnbar },
                new SqlParameter("@idAnbar2", SqlDbType.Int) { Value = request.IdAnbar },
                new SqlParameter("@idSandogh", SqlDbType.Int) { Value = request.IdSandogh },
                new SqlParameter("@idSandoghType", SqlDbType.Int) { Value = request.IdSandoghType },
                new SqlParameter("@sefareshID", SqlDbType.VarChar, 50) { Value = request.SefareshID! },
                new SqlParameter("@idSal", SqlDbType.Int) { Value = header.IdSal },
                new SqlParameter("@id", SqlDbType.VarChar, 10) { Value = header.Id });

            foreach (var item in request.Items)
            {
                var product = products[item.IdKala.Trim()];
                var unitPrice = item.UnitPrice ?? product.MabFrosh;
                await AddDetailAsync(
                    request,
                    header.IdSal,
                    header.Id,
                    item,
                    unitPrice,
                    transaction,
                    ct);
            }

            await transaction.CommitAsync(ct);

            var persisted = await _legacy.GetAsync(header.IdSal, header.Id, ct);
            if (!persisted.Success || persisted.Data == null)
                throw new ApiException(500, "DOCUMENT_RESPONSE_LOAD_FAILED", "سند وب ثبت شد اما قابل بازیابی نبود.");

            return ApiResponse<DocumentResponse>.SuccessResult(
                persisted.Data,
                "سفارش وبسایت به‌عنوان سند در انتظار تأیید ثبت شد.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<(int IdSal, string Id, int IdFaktor)> CreateHeaderAsync(
        CreateDocumentRequest request,
        IDbContextTransaction transaction,
        CancellationToken ct)
    {
        await using var command = CreateProcedureCommand(
            "dbo.AndCrFaktor",
            transaction.GetDbTransaction());

        var idSal = new SqlParameter("@IDSal", SqlDbType.Int)
        {
            Direction = ParameterDirection.InputOutput,
            Value = request.IdSal
        };

        var id = new SqlParameter("@ID", SqlDbType.VarChar, 10)
        {
            Direction = ParameterDirection.InputOutput,
            Value = string.Empty
        };

        var factor = new SqlParameter("@IDFaktor", SqlDbType.Int)
        {
            Direction = ParameterDirection.InputOutput,
            Value = 0
        };

        command.Parameters.Add(new SqlParameter("@UserID", SqlDbType.Int) { Value = request.IdMasool });
        command.Parameters.Add(new SqlParameter("@IDTaraf", SqlDbType.Int) { Value = request.IdTaraf });
        command.Parameters.Add(new SqlParameter("@Miz", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(idSal);
        command.Parameters.Add(id);
        command.Parameters.Add(factor);
        command.Parameters.Add(new SqlParameter("@GpsLat", SqlDbType.Float) { Value = 0d });
        command.Parameters.Add(new SqlParameter("@GpsLong", SqlDbType.Float) { Value = 0d });
        command.Parameters.Add(new SqlParameter("@TasvieType", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@TasvieCheck", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@Sharh", SqlDbType.VarChar, 200) { Value = (object?)request.Sharh ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@SanadType", SqlDbType.Int) { Value = request.SanadType });
        command.Parameters.Add(new SqlParameter("@IDSanadEx", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@IDSanadEx2", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@IDSanadEx3", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@IDFoodMahal", SqlDbType.Int) { Value = 1 });
        command.Parameters.Add(new SqlParameter("@Add", SqlDbType.VarChar, 100) { Value = string.Empty });
        command.Parameters.Add(new SqlParameter("@Tell", SqlDbType.VarChar, 50) { Value = string.Empty });
        command.Parameters.Add(new SqlParameter("@Des", SqlDbType.VarChar, 90) { Value = (object?)request.Des ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@TarafName", SqlDbType.VarChar, 30) { Value = string.Empty });
        command.Parameters.Add(new SqlParameter("@MabEzaf", SqlDbType.Decimal) { Precision = 18, Scale = 3, Value = 0m });
        command.Parameters.Add(new SqlParameter("@MabEzafOnvan", SqlDbType.VarChar, 30) { Value = string.Empty });
        command.Parameters.Add(new SqlParameter("@IDSalMabna", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@IDsanadMabna", SqlDbType.VarChar, 50) { Value = string.Empty });

        await command.ExecuteNonQueryAsync(ct);

        var createdIdSal = Convert.ToInt32(idSal.Value);
        var createdId = Convert.ToString(id.Value)?.Trim() ?? string.Empty;
        var createdFactor = Convert.ToInt32(factor.Value);

        if (string.IsNullOrWhiteSpace(createdId))
            throw new ApiException(500, "DOCUMENT_CREATE_FAILED", "شماره داخلی سند توسط پایگاه داده تولید نشد.");

        return (createdIdSal, createdId, createdFactor);
    }

    private static async Task<int> AddDetailAsync(
        CreateDocumentRequest request,
        int idSal,
        string idSanad,
        CreateDocumentItemRequest item,
        decimal unitPrice,
        IDbContextTransaction transaction,
        CancellationToken ct)
    {
        await using var command = CreateProcedureCommand(
            "dbo.AndCrFaktorKala",
            transaction.GetDbTransaction());

        var id2 = new SqlParameter("@ID2", SqlDbType.Int)
        {
            Direction = ParameterDirection.InputOutput,
            Value = 0
        };

        command.Parameters.Add(new SqlParameter("@IDSal", SqlDbType.Int) { Value = idSal });
        command.Parameters.Add(new SqlParameter("@IDSanad", SqlDbType.VarChar, 10) { Value = idSanad });
        command.Parameters.Add(new SqlParameter("@IDKala", SqlDbType.VarChar, 20) { Value = item.IdKala.Trim() });
        command.Parameters.Add(new SqlParameter("@Bes", SqlDbType.Float) { Value = (double)item.Quantity });
        command.Parameters.Add(new SqlParameter("@Des", SqlDbType.VarChar, 50) { Value = (object?)item.Description ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@BesMab", SqlDbType.Decimal) { Precision = 18, Scale = 3, Value = unitPrice });
        command.Parameters.Add(new SqlParameter("@TakhfifDarsad", SqlDbType.Float) { Value = 0d });
        command.Parameters.Add(new SqlParameter("@IDSanjesh", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@IDSanjesh2", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@BedBesZarib", SqlDbType.Float) { Value = 1d });
        command.Parameters.Add(new SqlParameter("@AtfNum", SqlDbType.VarChar, 50) { Value = string.Empty });
        command.Parameters.Add(new SqlParameter("@SanadType", SqlDbType.Int) { Value = request.SanadType });
        command.Parameters.Add(new SqlParameter("@SanadTypeNew", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(id2);
        command.Parameters.Add(new SqlParameter("@IDAttribValuesStock", SqlDbType.VarChar, 50) { Value = string.Empty });
        command.Parameters.Add(new SqlParameter("@Des3", SqlDbType.VarChar, 200) { Value = string.Empty });

        await command.ExecuteNonQueryAsync(ct);
        return Convert.ToInt32(id2.Value);
    }

    private static DbCommand CreateProcedureCommand(string procedure, DbTransaction transaction)
    {
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("SQL connection is not available.");

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedure;
        command.CommandTimeout = 90;
        return command;
    }

    private static async Task ExecuteSqlAsync(
        string sql,
        IDbContextTransaction transaction,
        CancellationToken ct,
        params SqlParameter[] parameters)
    {
        await using var command = CreateProcedureCommand(string.Empty, transaction.GetDbTransaction());
        command.CommandType = CommandType.Text;
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void ValidatePendingRequest(CreateDocumentRequest request)
    {
        if (request.Items.Count == 0)
            throw new ApiException(400, "EMPTY_DOCUMENT", "سند حداقل باید یک قلم داشته باشد.");
        if (request.IdSal <= 0)
            throw new ApiException(400, "INVALID_FISCAL_YEAR", "سال مالی معتبر نیست.");
        if (request.SanadType <= 0)
            throw new ApiException(400, "INVALID_DOCUMENT_TYPE", "نوع سند معتبر نیست.");
        if (string.IsNullOrWhiteSpace(request.SabtDate) || request.SabtDate.Length != 10)
            throw new ApiException(400, "INVALID_DATE", "تاریخ سند باید به صورت yyyy/MM/dd باشد.");
        if (request.IdMasool <= 0)
            throw new ApiException(400, "INVALID_USER", "کاربر مسئول سند معتبر نیست.");
        if (string.IsNullOrWhiteSpace(request.SefareshID))
            throw new ApiException(400, "INVALID_ORDER_NUMBER", "شماره سفارش وب معتبر نیست.");
    }
}
