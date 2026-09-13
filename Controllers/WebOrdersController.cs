using System.Data;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web-orders")]
public sealed class WebOrdersController : ControllerBase
{
    // KianStore uses SanadType=51 as the temporary/pending state that is later
    // consumed by SetFaktorFinalNew and converted to the final sale type (12).
    private const int PendingSanadType = 51;
    private const int FinalSaleSanadType = 12;

    private readonly KianStoreDbContext _context;
    private readonly IDocumentService _documentService;
    private readonly IStockService _stockService;

    public WebOrdersController(
        KianStoreDbContext context,
        IDocumentService documentService,
        IStockService stockService)
    {
        _context = context;
        _documentService = documentService;
        _stockService = stockService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] WebOrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "ORDER_ITEMS_REQUIRED",
                "حداقل یک کالا برای ثبت سفارش لازم است."));
        }

        if (!request.TarafId.HasValue || !request.TarafType.HasValue)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "CUSTOMER_REQUIRED",
                "برای ثبت سفارش وب، طرف حساب الزامی است."));
        }

        var idSal = request.IdSal ?? await GetCurrentIdSalAsync(cancellationToken);
        if (idSal <= 0)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult(
                "FISCAL_YEAR_NOT_FOUND",
                "سال مالی جاری در دیتابیس پیدا نشد."));
        }

        var idAnbar = request.IdAnbar ?? 1;
        var requestedItems = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.KalaId) && x.Quantity > 0)
            .GroupBy(x => x.KalaId.Trim(), StringComparer.Ordinal)
            .Select(g => new
            {
                KalaId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        if (requestedItems.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "ORDER_ITEMS_INVALID",
                "اقلام سفارش معتبر نیستند."));
        }

        var kalaIds = requestedItems.Select(x => x.KalaId).ToArray();
        var kalas = await _context.Kalas
            .AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id) && !x.IsDisabled)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var missing = kalaIds.FirstOrDefault(x => !kalas.ContainsKey(x));
        if (missing is not null)
        {
            return NotFound(ApiResponse<object>.ErrorResult(
                "PRODUCT_NOT_FOUND",
                $"کالا با شناسه {missing} یافت نشد."));
        }

        var tarafExists = await _context.Tarafs
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == request.TarafId.Value &&
                     x.IdType == request.TarafType.Value &&
                     !x.IsDisabled,
                cancellationToken);

        if (!tarafExists)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "CUSTOMER_NOT_FOUND",
                "طرف حساب انتخاب‌شده یافت نشد."));
        }

        foreach (var item in requestedItems)
        {
            var stock = await _stockService.CheckAsync(
                item.KalaId,
                item.Quantity,
                idAnbar,
                idSal,
                cancellationToken);

            if (!stock.IsAvailable)
            {
                return Conflict(ApiResponse<object>.ErrorResult(
                    "INSUFFICIENT_STOCK",
                    $"موجودی کالای {item.KalaId} کافی نیست.",
                    new
                    {
                        stock.Available,
                        stock.Requested,
                        stock.KalaId,
                        stock.IdAnbar,
                        stock.IdSal
                    }));
            }
        }

        var orderNumber = BuildOrderNumber(DateTime.UtcNow);
        var documentRequest = new CreateDocumentRequest
        {
            IdSal = idSal,
            SanadType = PendingSanadType,
            IdAnbar = idAnbar,
            IdTaraf = request.TarafId.Value,
            IdTarafType = request.TarafType.Value,
            IdMasool = 101,
            IdSandogh = 1,
            IdSandoghType = 1,
            SabtDate = DateTime.Now.ToString("yyyy/MM/dd"),
            Des = $"سفارش وبسایت - {orderNumber}",
            Sharh = request.Notes?.Trim(),
            CheckStock = false,
            IsPending = true,
            SefareshID = orderNumber,
            Items = requestedItems.Select(item => new CreateDocumentItemRequest
            {
                IdKala = item.KalaId,
                Quantity = item.Quantity,
                UnitPrice = kalas[item.KalaId].MabFrosh,
                PurchasePrice = null,
                IsIncoming = false
            }).ToList()
        };

        var document = await _documentService.CreateAsync(documentRequest, cancellationToken);
        if (!document.Success || document.Data is null)
        {
            return StatusCode(500, document);
        }

        return Ok(ApiResponse<object>.SuccessResult(
            new
            {
                document.Data.IdSal,
                document.Data.Id,
                document.Data.IdFaktor,
                OrderNumber = orderNumber,
                SanadType = PendingSanadType,
                document.Data.TotalAmount
            },
            "سفارش مستقیماً به سند در انتظار تأیید ثبت شد."));
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<object>>> GetPending(
        CancellationToken cancellationToken)
    {
        var idSal = await GetCurrentIdSalAsync(cancellationToken);
        if (idSal <= 0)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult(
                "FISCAL_YEAR_NOT_FOUND",
                "سال مالی جاری در دیتابیس پیدا نشد."));
        }

        var sanads = await _context.Sanads
            .AsNoTracking()
            .Where(x =>
                x.IdSal == idSal &&
                x.SanadType == PendingSanadType &&
                !x.Disable &&
                x.SefareshID != null &&
                x.SefareshID != "")
            .OrderByDescending(x => x.IdFaktor)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (sanads.Count == 0)
        {
            return Ok(ApiResponse<object>.SuccessResult(
                Array.Empty<object>(),
                "سند وب در انتظار تأیید وجود ندارد."));
        }

        var ids = sanads.Select(x => x.Id).ToList();
        var details = await _context.SanadDetails
            .AsNoTracking()
            .Where(x => x.IdSal == idSal && ids.Contains(x.IdSanad))
            .OrderBy(x => x.IdSanad)
            .ThenBy(x => x.Id2)
            .ToListAsync(cancellationToken);

        var kalaIds = details.Select(x => x.IdKala).Distinct().ToList();
        var kalas = await _context.Kalas
            .AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var tarafIds = sanads.Select(x => x.IdTaraf).Distinct().ToList();
        var tarafs = await _context.Tarafs
            .AsNoTracking()
            .Where(x => tarafIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var lookup = details.ToLookup(x => x.IdSanad, StringComparer.Ordinal);

        var result = sanads.Select(s =>
        {
            var taraf = tarafs.FirstOrDefault(t =>
                t.Id == s.IdTaraf && t.IdType == s.IdTarafType);

            return new
            {
                s.IdSal,
                s.Id,
                OrderNumber = s.SefareshID!,
                s.IdFaktor,
                s.SanadType,
                s.IdAnbar,
                s.IdTaraf,
                s.IdTarafType,
                TarafName = taraf?.Name,
                Mobile = taraf?.Mobile,
                Address = taraf?.Address,
                SabtDate = s.SabtDate,
                TotalAmount = s.MabKol,
                Description = s.Des,
                Notes = s.Sharh,
                Items = lookup[s.Id].Select(d => new
                {
                    Id = d.Id2,
                    d.IdKala,
                    KalaName = kalas.TryGetValue(d.IdKala, out var k)
                        ? k.KalaName
                        : d.IdKala,
                    Quantity = d.Bes2 > 0 ? d.Bes2 : d.Bes,
                    UnitPrice = d.BesMab2 > 0 ? d.BesMab2 : d.BesMab,
                    TotalPrice = d.SumMab,
                    PurchasePrice = d.BedMabKharid
                })
            };
        }).ToList();

        return Ok(ApiResponse<object>.SuccessResult(
            result,
            "فاکتورهای وب در انتظار تأیید دریافت شد."));
    }

    [HttpPost("{orderNumber}/finalize")]
    public async Task<ActionResult<ApiResponse<object>>> Finalize(
        string orderNumber,
        [FromBody] WebPendingOrderFinalizeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "ORDER_NUMBER_REQUIRED",
                "شماره سفارش الزامی است."));
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "PURCHASE_PRICES_REQUIRED",
                "قیمت خرید اقلام الزامی است."));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            // Keep the document at type 51 until SetFaktorFinalNew runs.
            // That procedure explicitly looks for SanadType=51 and performs
            // the official transition to the final sale type (12).
            var sanad = await _context.Sanads
                .FirstOrDefaultAsync(
                    x => x.SefareshID == orderNumber &&
                         x.SanadType == PendingSanadType &&
                         !x.Disable,
                    cancellationToken);

            if (sanad is null)
            {
                var alreadyFinal = await _context.Sanads
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.SefareshID == orderNumber &&
                             x.SanadType == FinalSaleSanadType &&
                             !x.Disable,
                        cancellationToken);

                await transaction.RollbackAsync(cancellationToken);

                if (alreadyFinal)
                {
                    return Conflict(ApiResponse<object>.ErrorResult(
                        "ORDER_ALREADY_FINALIZED",
                        "این سند قبلاً تأیید شده است."));
                }

                return NotFound(ApiResponse<object>.ErrorResult(
                    "ORDER_NOT_FOUND",
                    "سند وب در انتظار تأیید یافت نشد."));
            }

            var details = await _context.SanadDetails
                .Where(x => x.IdSal == sanad.IdSal && x.IdSanad == sanad.Id)
                .OrderBy(x => x.Id2)
                .ToListAsync(cancellationToken);

            if (details.Count == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict(ApiResponse<object>.ErrorResult(
                    "ORDER_DETAILS_NOT_FOUND",
                    "سفارش پیدا شد اما اقلام سند موجود نیستند."));
            }

            var prices = request.Items
                .Where(x => !string.IsNullOrWhiteSpace(x.KalaId))
                .GroupBy(x => x.KalaId.Trim(), StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Last().PurchasePrice, StringComparer.Ordinal);

            var missing = details
                .Where(x => !prices.TryGetValue(x.IdKala, out var p) || p <= 0)
                .Select(x => x.IdKala)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (missing.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(ApiResponse<object>.ErrorResult(
                    "PURCHASE_PRICES_REQUIRED",
                    "برای همه اقلام قیمت خرید واحد وارد شود.",
                    new { KalaIds = missing }));
            }

            foreach (var detail in details)
            {
                detail.BedMabKharid = prices[detail.IdKala];
                detail.SanadType = PendingSanadType;
            }

            sanad.SanadType = PendingSanadType;
            sanad.IsFinal = false;
            sanad.IsSavedFinal = false;

            if (!string.IsNullOrWhiteSpace(request.SabtDate))
                sanad.SabtDate = request.SabtDate;
            if (!string.IsNullOrWhiteSpace(request.Des))
                sanad.Des = request.Des;
            if (!string.IsNullOrWhiteSpace(request.Sharh))
                sanad.Sharh = request.Sharh;

            await _context.SaveChangesAsync(cancellationToken);

            await FinalizeWithKianStoreProcedureAsync(
                sanad.IdSal,
                sanad.Id,
                sanad.SabtDate,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var persisted = await _context.Sanads
                .AsNoTracking()
                .FirstAsync(
                    x => x.IdSal == sanad.IdSal && x.Id == sanad.Id,
                    cancellationToken);

            return Ok(ApiResponse<object>.SuccessResult(
                new
                {
                    persisted.IdSal,
                    persisted.Id,
                    persisted.IdFaktor,
                    persisted.IDSanad,
                    persisted.SanadType,
                    persisted.IsFinal,
                    persisted.IsSavedFinal,
                    persisted.MabKol
                },
                "همان سند وب با موفقیت تأیید و نهایی شد."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<int> GetCurrentIdSalAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT ISNULL(MAX(ID), 0) FROM dbo.SalMali;";
            command.CommandType = CommandType.Text;
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task FinalizeWithKianStoreProcedureAsync(
        int idSal,
        string idSanad,
        string sabtDate,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.SetFaktorFinalNew";
            command.CommandType = CommandType.StoredProcedure;
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 60;

            AddParameter(command, "@IDSal", DbType.Int32, idSal);
            AddParameter(command, "@IDSanad", DbType.String, idSanad);
            AddParameter(command, "@SabtDate", DbType.String, sabtDate);
            AddParameter(command, "@IsNaghd", DbType.Boolean, false);

            var sanadTime = command.CreateParameter();
            sanadTime.ParameterName = "@SanadTime";
            sanadTime.DbType = DbType.String;
            sanadTime.Size = 20;
            sanadTime.Direction = ParameterDirection.Output;
            command.Parameters.Add(sanadTime);

            var isSavedFinal = command.CreateParameter();
            isSavedFinal.ParameterName = "@IsSavedFinal";
            isSavedFinal.DbType = DbType.Boolean;
            isSavedFinal.Direction = ParameterDirection.Output;
            command.Parameters.Add(isSavedFinal);

            var errorMessage = command.CreateParameter();
            errorMessage.ParameterName = "@ErrMsg";
            errorMessage.DbType = DbType.String;
            errorMessage.Size = 100;
            errorMessage.Direction = ParameterDirection.Output;
            command.Parameters.Add(errorMessage);

            await command.ExecuteNonQueryAsync(cancellationToken);

            var err = errorMessage.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(err))
                throw new InvalidOperationException($"خطا در نهایی‌سازی سند در KianStore: {err}");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AddParameter(
        IDbCommand command,
        string name,
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string BuildOrderNumber(DateTime utcNow)
        => $"W{utcNow:yyyyMMddHHmmssfff}";
}
