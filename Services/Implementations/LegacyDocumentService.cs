using System.Data;
using System.Data.Common;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KianStore.Api.Services.Implementations;

/// <summary>
/// Writes and reads Sanad through the legacy KianStore database procedures.
/// This is intentional: the existing database owns Sanad/stock identifier
/// generation and derived fields through AndCrFaktor, AndCrFaktorKala and
/// SetFaktorFinal.
/// </summary>
public sealed class LegacyDocumentService : IDocumentService
{
    private readonly KianStoreDbContext _context;
    private readonly IStockService _stockService;

    public LegacyDocumentService(KianStoreDbContext context, IStockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    public async Task<ApiResponse<DocumentResponse>> CreateAsync(
        CreateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var tarafExists = await _context.Tarafs.AsNoTracking()
            .AnyAsync(x => x.Id == request.IdTaraf &&
                           x.IdType == request.IdTarafType &&
                           !x.IsDisabled,
                cancellationToken);

        if (!tarafExists)
            throw new ApiException(404, "CUSTOMER_NOT_FOUND", "طرف حساب مورد نظر یافت نشد.");

        var kalaIds = request.Items
            .Select(x => x.IdKala.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var products = await _context.Kalas.AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var stockWarnings = new List<object>();

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ApiException(400, "INVALID_QUANTITY", $"تعداد کالای {item.IdKala} باید بیشتر از صفر باشد.");

            if (!products.TryGetValue(item.IdKala.Trim(), out var product))
                throw new ApiException(404, "PRODUCT_NOT_FOUND", $"کالا با کد {item.IdKala} یافت نشد.");

            if (product.IsDisabled)
                throw new ApiException(409, "PRODUCT_DISABLED", $"کالای {item.IdKala} غیرفعال است.");

            if (!item.IsIncoming && request.CheckStock)
            {
                var stock = await _stockService.CheckAsync(
                    item.IdKala,
                    item.Quantity,
                    request.IdAnbar,
                    request.IdSal,
                    cancellationToken);

                if (!stock.IsAvailable)
                {
                    stockWarnings.Add(new
                    {
                        code = "INSUFFICIENT_STOCK",
                        message = $"موجودی کالای {item.IdKala} کافی نیست.",
                        stock.KalaId,
                        stock.IdAnbar,
                        stock.IdSal,
                        stock.Requested,
                        stock.Available,
                        stock.IsAvailable
                    });
                }
            }
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var header = await CreateHeaderAsync(request, transaction, cancellationToken);

            // Keep the API-selected warehouse/cashbox values in the header. The
            // detail procedure still calculates its legacy warehouse defaults,
            // which is required for compatibility with the old application.
            await ExecuteTextAsync(
                "UPDATE dbo.Sanad SET IDAnbar=@idAnbar, IDAnbar2=@idAnbar2, IDSandogh=@idSandogh, IDSandoghType=@idSandoghType WHERE IDSal=@idSal AND ID=@id",
                transaction,
                cancellationToken,
                new SqlParameter("@idAnbar", SqlDbType.Int) { Value = request.IdAnbar },
                new SqlParameter("@idAnbar2", SqlDbType.Int) { Value = request.IdAnbar },
                new SqlParameter("@idSandogh", SqlDbType.Int) { Value = request.IdSandogh },
                new SqlParameter("@idSandoghType", SqlDbType.Int) { Value = request.IdSandoghType },
                new SqlParameter("@idSal", SqlDbType.Int) { Value = header.IdSal },
                new SqlParameter("@id", SqlDbType.VarChar, 10) { Value = header.Id });

            foreach (var item in request.Items)
            {
                var product = products[item.IdKala.Trim()];
                var unitPrice = item.UnitPrice ?? (item.IsIncoming ? product.MabKharid : product.MabFrosh);

                await AddDetailAsync(
                    request,
                    header.IdSal,
                    header.Id,
                    item,
                    unitPrice,
                    transaction,
                    cancellationToken);
            }

            var finalized = await FinalizeAsync(
                request,
                header.IdSal,
                header.Id,
                header.IdFaktor,
                transaction,
                cancellationToken);

            if (!finalized.IsSavedFinal)
            {
                throw new ApiException(
                    409,
                    "DOCUMENT_FINALIZATION_FAILED",
                    string.IsNullOrWhiteSpace(finalized.ErrorMessage)
                        ? "فاکتور ایجاد شد اما نهایی‌سازی سند انجام نشد."
                        : finalized.ErrorMessage);
            }

            await transaction.CommitAsync(cancellationToken);

            var persisted = await GetAsync(header.IdSal, header.Id, cancellationToken);
            if (!persisted.Success || persisted.Data == null)
                throw new ApiException(500, "DOCUMENT_RESPONSE_LOAD_FAILED", "سند ثبت شد اما اطلاعات نهایی آن قابل بازیابی نبود.");

            if (stockWarnings.Count > 0)
            {
                return ApiResponse<DocumentResponse>.SuccessWithWarningResult(
                    persisted.Data,
                    stockWarnings,
                    "سند ثبت شد، اما موجودی یک یا چند کالا کافی نبود.",
                    "STOCK_WARNING");
            }

            var message = request.Items.Any(x => x.IsIncoming)
                ? "سند خرید با موفقیت ثبت شد و موجودی کالا افزایش یافت."
                : "فاکتور با موفقیت ثبت و نهایی شد.";

            return ApiResponse<DocumentResponse>.SuccessResult(persisted.Data, message);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApiResponse<DocumentResponse>> GetAsync(
        int idSal,
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ApiException(400, "INVALID_DOCUMENT_ID", "شناسه سند معتبر نیست.");

        var sanad = await _context.Sanads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdSal == idSal && x.Id == id, cancellationToken);

        if (sanad == null)
            throw new ApiException(404, "DOCUMENT_NOT_FOUND", "سند مورد نظر یافت نشد.");

        var details = await _context.SanadDetails.AsNoTracking()
            .Where(x => x.IdSal == idSal && x.IdSanad == id)
            .OrderBy(x => x.Id2)
            .ToListAsync(cancellationToken);

        var tarafName = await _context.Tarafs.AsNoTracking()
            .Where(x => x.Id == sanad.IdTaraf && x.IdType == sanad.IdTarafType)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return ApiResponse<DocumentResponse>.SuccessResult(Map(sanad, details, tarafName));
    }

    public async Task<ApiResponse<IReadOnlyList<DocumentResponse>>> GetHistoryAsync(
        int idSal,
        int sanadType = 12,
        int page = 1,
        int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        if (idSal <= 0)
            throw new ApiException(400, "INVALID_FISCAL_YEAR", "سال مالی معتبر نیست.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Sanad> query = _context.Sanads.AsNoTracking()
            .Where(x => x.IdSal == idSal && !x.Disable);

        if (sanadType > 0)
            query = query.Where(x => x.SanadType == sanadType);

        var sanads = await query
            .OrderByDescending(x => x.IdFaktor)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (sanads.Count == 0)
        {
            return ApiResponse<IReadOnlyList<DocumentResponse>>.SuccessResult(
                Array.Empty<DocumentResponse>(),
                "تاریخچه سندی ندارد.");
        }

        var ids = sanads.Select(x => x.Id).ToList();
        var details = await _context.SanadDetails.AsNoTracking()
            .Where(x => x.IdSal == idSal && ids.Contains(x.IdSanad))
            .OrderBy(x => x.IdSanad)
            .ThenBy(x => x.Id2)
            .ToListAsync(cancellationToken);

        var tarafIds = sanads.Select(x => x.IdTaraf).Distinct().ToList();
        var tarafs = await _context.Tarafs.AsNoTracking()
            .Where(x => tarafIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var detailLookup = details.ToLookup(x => x.IdSanad);
        var result = sanads.Select(s =>
        {
            var tarafName = tarafs.FirstOrDefault(t => t.Id == s.IdTaraf && t.IdType == s.IdTarafType)?.Name;
            return Map(s, detailLookup[s.Id].ToList(), tarafName);
        }).ToList();

        return ApiResponse<IReadOnlyList<DocumentResponse>>.SuccessResult(
            result,
            "تاریخچه اسناد با موفقیت دریافت شد.");
    }

    private async Task<(int IdSal, string Id, int IdFaktor)> CreateHeaderAsync(
        CreateDocumentRequest request,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateProcedureCommand("dbo.AndCrFaktor", transaction.GetDbTransaction());

        var idSal = new SqlParameter("@IDSal", SqlDbType.Int)
        {
            Direction = ParameterDirection.InputOutput,
            Value = request.IdSal
        };
        var id = new SqlParameter("@ID", SqlDbType.VarChar, 10)
        {
            Direction = ParameterDirection.InputOutput,
            Value = ""
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
        command.Parameters.Add(new SqlParameter("@Add", SqlDbType.VarChar, 100) { Value = "" });
        command.Parameters.Add(new SqlParameter("@Tell", SqlDbType.VarChar, 50) { Value = "" });
        command.Parameters.Add(new SqlParameter("@Des", SqlDbType.VarChar, 90) { Value = (object?)request.Des ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@TarafName", SqlDbType.VarChar, 30) { Value = "" });
        command.Parameters.Add(new SqlParameter("@MabEzaf", SqlDbType.Decimal) { Precision = 18, Scale = 3, Value = 0m });
        command.Parameters.Add(new SqlParameter("@MabEzafOnvan", SqlDbType.VarChar, 30) { Value = "" });
        command.Parameters.Add(new SqlParameter("@IDSalMabna", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@IDsanadMabna", SqlDbType.VarChar, 50) { Value = "" });

        await command.ExecuteNonQueryAsync(cancellationToken);

        var createdIdSal = Convert.ToInt32(idSal.Value);
        var createdId = Convert.ToString(id.Value)?.Trim() ?? string.Empty;
        var createdFactor = Convert.ToInt32(factor.Value);

        if (string.IsNullOrWhiteSpace(createdId))
            throw new ApiException(500, "DOCUMENT_CREATE_FAILED", "شماره داخلی سند توسط پایگاه داده تولید نشد.");

        return (createdIdSal, createdId, createdFactor);
    }

    private static async Task AddDetailAsync(
        CreateDocumentRequest request,
        int idSal,
        string idSanad,
        CreateDocumentItemRequest item,
        decimal unitPrice,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateProcedureCommand("dbo.AndCrFaktorKala", transaction.GetDbTransaction());

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
        command.Parameters.Add(new SqlParameter("@AtfNum", SqlDbType.VarChar, 50) { Value = "" });
        command.Parameters.Add(new SqlParameter("@SanadType", SqlDbType.Int) { Value = request.SanadType });
        command.Parameters.Add(new SqlParameter("@SanadTypeNew", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(id2);
        command.Parameters.Add(new SqlParameter("@IDAttribValuesStock", SqlDbType.VarChar, 50) { Value = "" });
        command.Parameters.Add(new SqlParameter("@Des3", SqlDbType.VarChar, 200) { Value = "" });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(bool IsSavedFinal, string ErrorMessage)> FinalizeAsync(
        CreateDocumentRequest request,
        int idSal,
        string id,
        int idFaktor,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateProcedureCommand("dbo.SetFaktorFinal", transaction.GetDbTransaction());

        var sanadTime = new SqlParameter("@SanadTime", SqlDbType.VarChar, 10)
        {
            Direction = ParameterDirection.Output
        };
        var factor = new SqlParameter("@IDFaktor", SqlDbType.Int)
        {
            Direction = ParameterDirection.InputOutput,
            Value = idFaktor
        };
        var idSanad = new SqlParameter("@IDSanad", SqlDbType.Int)
        {
            Direction = ParameterDirection.InputOutput,
            Value = 0
        };
        var isSavedFinal = new SqlParameter("@IsSavedFinal", SqlDbType.Bit)
        {
            Direction = ParameterDirection.Output
        };
        var errorMessage = new SqlParameter("@ErrMsg", SqlDbType.VarChar, 100)
        {
            Direction = ParameterDirection.Output
        };
        var idSanadAtf = new SqlParameter("@IDSanadAtf", SqlDbType.Int)
        {
            Direction = ParameterDirection.InputOutput,
            Value = 0
        };

        command.Parameters.Add(new SqlParameter("@IDSal", SqlDbType.Int) { Value = idSal });
        command.Parameters.Add(new SqlParameter("@ID", SqlDbType.VarChar, 20) { Value = id });
        command.Parameters.Add(new SqlParameter("@SabtDate", SqlDbType.VarChar, 10) { Value = request.SabtDate });
        command.Parameters.Add(sanadTime);
        command.Parameters.Add(factor);
        command.Parameters.Add(idSanad);
        command.Parameters.Add(new SqlParameter("@SanadType", SqlDbType.Int) { Value = request.SanadType });
        command.Parameters.Add(new SqlParameter("@SanadType2", SqlDbType.Int) { Value = request.SanadType });
        command.Parameters.Add(isSavedFinal);
        command.Parameters.Add(errorMessage);
        command.Parameters.Add(new SqlParameter("@MinIDFaktor", SqlDbType.Int) { Value = 1 });
        command.Parameters.Add(new SqlParameter("@MaxIDFaktor", SqlDbType.Int) { Value = 99999999 });
        command.Parameters.Add(new SqlParameter("@IDTaraf", SqlDbType.Int) { Value = request.IdTaraf });
        command.Parameters.Add(idSanadAtf);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return (
            isSavedFinal.Value != DBNull.Value && Convert.ToBoolean(isSavedFinal.Value),
            Convert.ToString(errorMessage.Value) ?? string.Empty);
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

    private static async Task ExecuteTextAsync(
        string sql,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken,
        params SqlParameter[] parameters)
    {
        var command = CreateProcedureCommand(string.Empty, transaction.GetDbTransaction());
        await using (command)
        {
            command.CommandType = CommandType.Text;
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void ValidateRequest(CreateDocumentRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new ApiException(400, "EMPTY_DOCUMENT", "سند حداقل باید یک قلم داشته باشد.");

        if (request.IdSal <= 0)
            throw new ApiException(400, "INVALID_FISCAL_YEAR", "سال مالی معتبر نیست.");

        if (request.SanadType <= 0)
            throw new ApiException(400, "INVALID_DOCUMENT_TYPE", "نوع سند معتبر نیست.");

        if (string.IsNullOrWhiteSpace(request.SabtDate) || request.SabtDate.Length != 10)
            throw new ApiException(400, "INVALID_DATE", "تاریخ سند باید به صورت yyyy/MM/dd باشد.");

        if (request.IdMasool <= 0)
            throw new ApiException(400, "INVALID_USER", "کاربر مسئول سند معتبر نیست.");
    }

    private static DocumentResponse Map(
        Sanad sanad,
        IReadOnlyCollection<SanadDetail> details,
        string? tarafName)
    {
        return new DocumentResponse
        {
            IdSal = sanad.IdSal,
            Id = sanad.Id,
            SanadType = sanad.SanadType,
            IdAnbar = sanad.IdAnbar,
            IdTaraf = sanad.IdTaraf,
            IdTarafType = sanad.IdTarafType,
            IdFaktor = sanad.IdFaktor,
            SabtDate = sanad.SabtDate,
            TotalAmount = sanad.MabKol,
            DiscountAmount = sanad.Takhfif,
            IsFinal = sanad.IsFinal || sanad.IsSavedFinal,
            Description = sanad.Des,
            TarafName = tarafName,
            Items = details.Select(x => new DocumentItemResponse
            {
                Id2 = x.Id2,
                IdKala = x.IdKala,
                Quantity = x.Bed2 > 0 ? x.Bed2 : x.Bes2,
                IsIncoming = x.Bed2 > 0,
                UnitPrice = x.Bed2 > 0 ? x.BedMab2 : x.BesMab2,
                TotalAmount = x.SumMab
            }).ToList()
        };
    }
}
