using System.Data;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.StockTransfers;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KianStore.Api.Services.Implementations;

public sealed class StockTransferService
{
    public const string EngineVersion = "native-sanad-6-7-v2";
    private const int SourceTransferType = 6;
    private const int DestinationTransferType = 7;

    private readonly KianStoreDbContext _context;
    private readonly IStockService _stockService;

    public StockTransferService(KianStoreDbContext context, IStockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    public async Task<ApiResponse<IReadOnlyList<StockTransferWarehouseResponse>>> GetWarehousesAsync(CancellationToken ct)
    {
        var warehouses = await _context.Anbars.AsNoTracking()
            .Where(x => x.Id > 0 && !x.NoActive)
            .OrderBy(x => x.Id == 1 ? 0 : 1)
            .ThenBy(x => x.Name)
            .Select(x => new StockTransferWarehouseResponse
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<StockTransferWarehouseResponse>>.SuccessResult(
            warehouses,
            "انبارها با موفقیت دریافت شدند.");
    }

    public async Task<ApiResponse<IReadOnlyList<StockTransferProductWarehouseInventoryResponse>>> GetProductInventoryByWarehousesAsync(
        int idSal,
        string idKala,
        CancellationToken ct)
    {
        idKala = idKala?.Trim() ?? string.Empty;
        if (idSal <= 0 || string.IsNullOrWhiteSpace(idKala))
            throw new ApiException(400, "INVALID_PRODUCT", "سال مالی یا کد کالا معتبر نیست.");

        var warehouses = await _context.Anbars.AsNoTracking()
            .Where(x => x.Id > 0 && !x.NoActive)
            .OrderBy(x => x.Id == 1 ? 0 : 1)
            .ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);

        var stocks = await (
            from detail in _context.SanadDetails.AsNoTracking()
            join sanad in _context.Sanads.AsNoTracking()
                on new { detail.IdSal, Id = detail.IdSanad } equals new { sanad.IdSal, sanad.Id }
            where detail.IdSal == idSal
                  && detail.IdKala == idKala
                  && !sanad.Disable
                  && detail.SanadType != 16
                  && detail.SanadType != 19
            group detail by detail.IdAnbar into g
            select new
            {
                IdAnbar = g.Key,
                Stock = g.Sum(x => x.Bed2 - x.Bes2)
            })
            .ToDictionaryAsync(x => x.IdAnbar, x => (decimal)x.Stock, ct);

        var result = warehouses
            .Select(x => new StockTransferProductWarehouseInventoryResponse
            {
                IdAnbar = x.Id,
                AnbarName = x.Name,
                Stock = stocks.TryGetValue(x.Id, out var stock) ? stock : 0m
            })
            .ToList();

        return ApiResponse<IReadOnlyList<StockTransferProductWarehouseInventoryResponse>>.SuccessResult(
            result,
            "موجودی کالا در انبارها با موفقیت دریافت شد.");
    }

    public async Task<ApiResponse<IReadOnlyList<StockTransferInventoryResponse>>> GetInventoryAsync(
        int idSal,
        int sourceAnbarId,
        CancellationToken ct)
    {
        if (idSal <= 0 || sourceAnbarId <= 0)
            throw new ApiException(400, "INVALID_WAREHOUSE", "سال مالی یا انبار مبدأ معتبر نیست.");

        var ids = await _context.KalaDetails.AsNoTracking()
            .Where(x => x.IdAnbar == sourceAnbarId && x.Quantity > 0)
            .Select(x => x.IdKala)
            .Union(
                _context.SanadDetails.AsNoTracking()
                    .Join(_context.Sanads.AsNoTracking(),
                        d => new { d.IdSal, Id = d.IdSanad },
                        s => new { s.IdSal, Id = s.Id },
                        (d, s) => new { d, s })
                    .Where(x => x.d.IdSal == idSal &&
                                x.d.IdAnbar == sourceAnbarId &&
                                !x.s.Disable)
                    .GroupBy(x => x.d.IdKala)
                    .Select(g => g.Key))
            .Distinct()
            .ToListAsync(ct);

        var products = await _context.Kalas.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && !x.IsDisabled)
            .OrderBy(x => x.KalaName)
            .ToListAsync(ct);

        var result = new List<StockTransferInventoryResponse>(products.Count);
        foreach (var product in products)
        {
            // Do not trust the optional KalaDetails cache here: a legacy row can exist with
            // Quantity = 0 while the real stock is present in SanadDetails.
            var stock = await CalculateStockFromDocumentsAsync(product.Id, sourceAnbarId, idSal, ct);
            if (stock <= 0) continue;

            result.Add(new StockTransferInventoryResponse
            {
                IdKala = product.Id,
                Name = product.KalaName,
                Stock = stock
            });
        }

        return ApiResponse<IReadOnlyList<StockTransferInventoryResponse>>.SuccessResult(
            result,
            "موجودی انبار با موفقیت دریافت شد.");
    }

    public async Task<ApiResponse<IReadOnlyList<StockTransferHistoryResponse>>> GetHistoryAsync(
        int idSal,
        int page,
        int pageSize,
        bool bookmarkedOnly,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Sanads.AsNoTracking()
            .Where(x => x.SanadType == SourceTransferType &&
                        !x.Disable &&
                        !(x.Des != null && x.Des.StartsWith("ابطال سند انتقال")));

        if (idSal > 0)
            query = query.Where(x => x.IdSal == idSal);

        if (bookmarkedOnly)
            query = query.Where(x => x.IsBookmarked);

        var sourceHeaders = await query
            .OrderByDescending(x => x.IdSal)
            .ThenByDescending(x => x.IdFaktor)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (sourceHeaders.Count == 0)
            return ApiResponse<IReadOnlyList<StockTransferHistoryResponse>>.SuccessResult(
                Array.Empty<StockTransferHistoryResponse>(),
                "تاریخچه انتقال بین انبارها خالی است.");

        var relatedIds = sourceHeaders
            .Select(x => GetRelatedSanadId(x.Id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var destinationHeaders = await _context.Sanads.AsNoTracking()
            .Where(x => x.SanadType == DestinationTransferType &&
                        sourceHeaders.Select(s => s.IdSal).Contains(x.IdSal) &&
                        relatedIds.Contains(x.Id))
            .ToListAsync(ct);

        var destinationByKey = destinationHeaders.ToDictionary(
            x => x.IdSal + "|" + x.Id,
            StringComparer.Ordinal);

        var sourceIds = sourceHeaders.Select(x => x.Id).ToList();
        var sourceDetails = await _context.SanadDetails.AsNoTracking()
            .Where(x => sourceHeaders.Select(s => s.IdSal).Contains(x.IdSal) &&
                        sourceIds.Contains(x.IdSanad) &&
                        x.SanadType == SourceTransferType)
            .OrderBy(x => x.IdSal)
            .ThenBy(x => x.IdSanad)
            .ThenBy(x => x.Id2)
            .ToListAsync(ct);

        var kalaIds = sourceDetails.Select(x => x.IdKala).Distinct(StringComparer.Ordinal).ToList();
        var productNames = await _context.Kalas.AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id))
            .Select(x => new { x.Id, x.KalaName })
            .ToDictionaryAsync(x => x.Id, x => x.KalaName, StringComparer.Ordinal, ct);

        var warehouseIds = sourceHeaders
            .Select(x => x.IdAnbar)
            .Concat(destinationHeaders.Select(x => x.IdAnbar))
            .Distinct()
            .ToList();

        var warehouseNames = await _context.Anbars.AsNoTracking()
            .Where(x => warehouseIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var detailLookup = sourceDetails.ToLookup(x => x.IdSal + "|" + x.IdSanad);

        var result = new List<StockTransferHistoryResponse>(sourceHeaders.Count);
        foreach (var source in sourceHeaders)
        {
            var relatedId = GetRelatedSanadId(source.Id);
            destinationByKey.TryGetValue(source.IdSal + "|" + relatedId, out var destination);

            var items = detailLookup[source.IdSal + "|" + source.Id]
                .GroupBy(x => x.IdKala, StringComparer.Ordinal)
                .Select(g => new StockTransferHistoryItemResponse
                {
                    IdKala = g.Key,
                    Name = productNames.TryGetValue(g.Key, out var name) ? name : g.Key,
                    Quantity = (decimal)g.Sum(x => x.Bes2 > 0 ? x.Bes2 : x.Bes)
                })
                .Where(x => x.Quantity > 0)
                .ToList();

            result.Add(new StockTransferHistoryResponse
            {
                IdSal = source.IdSal,
                Id = source.Id,
                IsBookmarked = source.IsBookmarked,
                IdFaktor = source.IdFaktor,
                SabtDate = source.SabtDate,
                Note = string.IsNullOrWhiteSpace(source.Sharh) ? source.Des : source.Sharh,
                SourceAnbarId = source.IdAnbar,
                SourceAnbarName = warehouseNames.TryGetValue(source.IdAnbar, out var sourceName) ? sourceName : $"انبار {source.IdAnbar}",
                DestinationAnbarId = destination?.IdAnbar ?? 0,
                DestinationAnbarName = destination == null
                    ? "—"
                    : (warehouseNames.TryGetValue(destination.IdAnbar, out var destinationName) ? destinationName : $"انبار {destination.IdAnbar}"),
                ItemCount = items.Count,
                Items = items
            });
        }

        return ApiResponse<IReadOnlyList<StockTransferHistoryResponse>>.SuccessResult(
            result,
            "تاریخچه انتقال بین انبارها با موفقیت دریافت شد.");
    }


    public async Task<ApiResponse<StockTransferBookmarkResponse>> SetBookmarkAsync(
        int idSal,
        string id,
        bool isBookmarked,
        CancellationToken ct)
    {
        if (idSal <= 0 || string.IsNullOrWhiteSpace(id))
            throw new ApiException(400, "INVALID_SANAD", "سال مالی یا شماره سند معتبر نیست.");

        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.Sanad
SET IsBookmarked = {isBookmarked}
WHERE IdSal = {idSal}
  AND Id = {id}
  AND SanadType = {SourceTransferType}
  AND Disable = 0;", ct);

        if (affected == 0)
            throw new ApiException(404, "TRANSFER_NOT_FOUND", "سند انتقال مورد نظر پیدا نشد یا غیرفعال است.");

        return ApiResponse<StockTransferBookmarkResponse>.SuccessResult(
            new StockTransferBookmarkResponse
            {
                IdSal = idSal,
                Id = id,
                IsBookmarked = isBookmarked,
                Message = isBookmarked
                    ? "سند انتقال نشان شد."
                    : "نشان سند انتقال برداشته شد."
            },
            isBookmarked
                ? "سند انتقال با موفقیت نشان شد."
                : "نشان سند انتقال با موفقیت برداشته شد.");
    }

    private static List<StockTransferItemRequest> NormalizeTransferItems(
        IEnumerable<StockTransferItemRequest> source)
        => source
            .Where(x => !string.IsNullOrWhiteSpace(x.IdKala))
            .GroupBy(x => x.IdKala.Trim(), StringComparer.Ordinal)
            .Select(g => new StockTransferItemRequest
            {
                IdKala = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .Where(x => x.Quantity > 0)
            .ToList();

    private async Task<LoadedTransfer> LoadActiveTransferAsync(
        int idSal,
        string id,
        CancellationToken ct)
    {
        var source = await _context.Sanads.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.IdSal == idSal &&
                     x.Id == id &&
                     x.SanadType == SourceTransferType &&
                     !x.Disable,
                ct);

        if (source == null)
            throw new ApiException(404, "TRANSFER_NOT_FOUND", "سند انتقال مورد نظر پیدا نشد یا قبلاً حذف شده است.");

        var destinationId = GetRelatedSanadId(source.Id);
        var destination = await _context.Sanads.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.IdSal == idSal &&
                     x.Id == destinationId &&
                     x.SanadType == DestinationTransferType &&
                     !x.Disable,
                ct);

        if (destination == null)
            throw new ApiException(409, "TRANSFER_PAIR_MISSING", "سند مقصد متناظر برای سند انتقال پیدا نشد.");

        var details = await _context.SanadDetails.AsNoTracking()
            .Where(x => x.IdSal == idSal &&
                        x.IdSanad == source.Id &&
                        x.SanadType == SourceTransferType)
            .ToListAsync(ct);

        var items = details
            .GroupBy(x => x.IdKala, StringComparer.Ordinal)
            .Select(g => new StockTransferItemRequest
            {
                IdKala = g.Key,
                Quantity = (decimal)g.Sum(x => x.Bes2 > 0 ? x.Bes2 : x.Bes)
            })
            .Where(x => x.Quantity > 0)
            .ToList();

        if (items.Count == 0)
            throw new ApiException(409, "TRANSFER_DETAILS_MISSING", "اقلام سند انتقال پیدا نشد.");

        return new LoadedTransfer(source, destination, items);
    }

    private async Task<StockTransferResponse> ReverseAndDisableTransferAsync(
        Sanad source,
        Sanad destination,
        IReadOnlyList<StockTransferItemRequest> items,
        CancellationToken ct)
    {
        var reverse = await ApplyTransferMovementAsync(
            source.IdSal,
            destination.IdAnbar,
            source.IdAnbar,
            source.SabtDate,
            $"ابطال سند انتقال {source.IdFaktor}",
            items,
            description: $"ابطال سند انتقال {source.IdFaktor}",
            ct);

        await DisableTransferHeadersAsync(
            source.IdSal,
            source.Id,
            destination.Id,
            ct);

        return reverse;
    }

    private async Task DisableTransferHeadersAsync(
        int idSal,
        string sourceId,
        string destinationId,
        CancellationToken ct)
    {
        const string sql = @"
UPDATE dbo.Sanad
SET Disable = 1,
    IsFinal = 0,
    IsSavedFinal = 0,
    ShowInSanad = 0,
    ShowInFaktor = 0
WHERE IdSal = @IdSal
  AND Id IN (@SourceId, @DestinationId);";

        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        AddParameter(command, "@IdSal", DbType.Int32, idSal);
        AddParameter(command, "@SourceId", DbType.AnsiString, sourceId);
        AddParameter(command, "@DestinationId", DbType.AnsiString, destinationId);

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<StockTransferResponse> ApplyTransferMovementAsync(
        int idSal,
        int sourceAnbarId,
        int destinationAnbarId,
        string sabtDate,
        string? note,
        IReadOnlyList<StockTransferItemRequest> items,
        string? description,
        CancellationToken ct)
    {
        if (sourceAnbarId <= 0 || destinationAnbarId <= 0 ||
            sourceAnbarId == destinationAnbarId)
            throw new ApiException(400, "INVALID_WAREHOUSE", "انبار مبدأ و مقصد را به‌درستی انتخاب کنید.");

        var warehouses = await _context.Anbars.AsNoTracking()
            .Where(x => x.Id == sourceAnbarId || x.Id == destinationAnbarId)
            .ToListAsync(ct);

        if (warehouses.Count != 2)
            throw new ApiException(404, "WAREHOUSE_NOT_FOUND", "انبار مبدأ یا مقصد پیدا نشد.");

        var productIds = items.Select(x => x.IdKala).ToList();
        var products = await _context.Kalas.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var neutralTarafExists = await _context.Tarafs.AsNoTracking()
            .AnyAsync(x => x.Id == 0 && x.IdType == 2, ct);

        var neutralUserId = await _context.Users.AsNoTracking()
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (!neutralTarafExists || !neutralUserId.HasValue)
            throw new ApiException(
                409,
                "TRANSFER_REFERENCE_DATA_MISSING",
                "رکوردهای پایه لازم برای ثبت سند انتقال موجود نیستند.");

        var nativeProcedureExists = await _context.Database
            .SqlQueryRaw<int>("SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.InsertTwoSanadRelated') AND type = 'P') THEN 1 ELSE 0 END AS [Value]")
            .SingleAsync(ct);

        if (nativeProcedureExists != 1)
            throw new ApiException(
                409,
                "TRANSFER_PROCEDURE_MISSING",
                "Procedure اصلی انتقال انبار در دیتابیس پیدا نشد.");

        var defaultCheckDefExists = await _context.CheckDefs.AsNoTracking()
            .AnyAsync(x => x.Id == 1 && x.Type == 1, ct);

        if (!defaultCheckDefExists)
            throw new ApiException(
                409,
                "TRANSFER_CHECKDEF_MISSING",
                "حساب پیش‌فرض انتقال در دیتابیس وجود ندارد.");

        var sourceStocks = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var destinationStocks = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.IdKala, out var product) || product.IsDisabled)
                throw new ApiException(404, "PRODUCT_NOT_FOUND", $"کالا با کد {item.IdKala} پیدا نشد.");

            var sourceStock = await CalculateStockFromDocumentsAsync(
                item.IdKala, sourceAnbarId, idSal, ct);

            if (sourceStock < item.Quantity)
                throw new ApiException(
                    409,
                    "INSUFFICIENT_STOCK",
                    $"موجودی «{product.KalaName}» در انبار مبدأ کافی نیست. موجودی: {sourceStock}، درخواست: {item.Quantity}.");

            sourceStocks[item.IdKala] = sourceStock;
            destinationStocks[item.IdKala] = await CalculateStockFromDocumentsAsync(
                item.IdKala, destinationAnbarId, idSal, ct);
        }

        var sanadId = await GenerateSanadIdAsync(idSal, ct);
        var secondSanadId = GetRelatedSanadId(sanadId);

        await InsertNativeTransferHeadersAsync(
            idSal,
            sanadId,
            secondSanadId,
            sourceAnbarId,
            destinationAnbarId,
            sabtDate,
            neutralUserId.Value,
            note,
            description,
            ct);

        var nativeHeaders = await _context.Sanads.AsNoTracking()
            .Where(x => x.IdSal == idSal && (x.Id == sanadId || x.Id == secondSanadId))
            .Select(x => new { x.Id, x.SanadType, x.IdAnbar })
            .ToListAsync(ct);

        var sourceHeader = nativeHeaders.FirstOrDefault(x => x.Id == sanadId);
        var destinationHeader = nativeHeaders.FirstOrDefault(x => x.Id == secondSanadId);

        if (sourceHeader == null || destinationHeader == null ||
            sourceHeader.SanadType != SourceTransferType ||
            destinationHeader.SanadType != DestinationTransferType ||
            sourceHeader.IdAnbar != sourceAnbarId ||
            destinationHeader.IdAnbar != destinationAnbarId)
        {
            throw new ApiException(
                409,
                "TRANSFER_NATIVE_HEADER_INVALID",
                "سندهای انتقال داخلی مطابق انبارهای مبدأ و مقصد ایجاد نشدند.");
        }

        var details = new List<SanadDetail>();
        var row = 1;
        foreach (var item in items)
        {
            var product = products[item.IdKala];
            var qty = (double)item.Quantity;

            details.Add(CreateDetail(
                idSal, sanadId, row++, product, sourceAnbarId,
                bed: 0, bes: qty, sanadType: SourceTransferType));

            details.Add(CreateDetail(
                idSal, secondSanadId, row++, product, destinationAnbarId,
                bed: qty, bes: 0, sanadType: DestinationTransferType));
        }

        await InsertTransferDetailsAsync(details, ct);

        foreach (var item in items)
        {
            await SetCachedStockAsync(
                item.IdKala,
                sourceAnbarId,
                sourceStocks[item.IdKala] - item.Quantity,
                products[item.IdKala],
                ct);

            await SetCachedStockAsync(
                item.IdKala,
                destinationAnbarId,
                destinationStocks[item.IdKala] + item.Quantity,
                products[item.IdKala],
                ct);
        }

        return new StockTransferResponse
        {
            IdSal = idSal,
            Id = sanadId,
            SourceAnbarId = sourceAnbarId,
            DestinationAnbarId = destinationAnbarId,
            ItemCount = items.Count,
            Message = string.IsNullOrWhiteSpace(description)
                ? $"انتقال موجودی با موفقیت ثبت شد. سند مبدأ: {sanadId}، سند مقصد: {secondSanadId}."
                : $"عملیات معکوس انتقال با موفقیت ثبت شد. سند: {sanadId}."
        };
    }

    private sealed record LoadedTransfer(
        Sanad Source,
        Sanad Destination,
        List<StockTransferItemRequest> Items);

    public async Task<ApiResponse<StockTransferResponse>> UpdateAsync(
        int idSal,
        string id,
        UpdateStockTransferRequest request,
        CancellationToken ct)
    {
        if (idSal <= 0 || string.IsNullOrWhiteSpace(id))
            throw new ApiException(400, "INVALID_TRANSFER", "شناسه سند انتقال معتبر نیست.");
        if (request.SourceAnbarId <= 0 || request.DestinationAnbarId <= 0 ||
            request.SourceAnbarId == request.DestinationAnbarId)
            throw new ApiException(400, "INVALID_WAREHOUSE", "انبار مبدأ و مقصد را به‌درستی انتخاب کنید.");
        if (string.IsNullOrWhiteSpace(request.SabtDate) || request.SabtDate.Length != 10)
            throw new ApiException(400, "INVALID_DATE", "تاریخ سند باید به صورت yyyy/MM/dd باشد.");

        var newItems = NormalizeTransferItems(request.Items);
        if (newItems.Count == 0)
            throw new ApiException(400, "EMPTY_TRANSFER", "حداقل یک کالا برای انتقال انتخاب کنید.");

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var original = await LoadActiveTransferAsync(idSal, id, ct);
            await ReverseAndDisableTransferAsync(original.Source, original.Destination, original.Items, ct);

            var result = await ApplyTransferMovementAsync(
                idSal,
                request.SourceAnbarId,
                request.DestinationAnbarId,
                request.SabtDate,
                request.Note,
                newItems,
                description: null,
                ct);

            await transaction.CommitAsync(ct);
            return ApiResponse<StockTransferResponse>.SuccessResult(
                new StockTransferResponse
                {
                    IdSal = result.IdSal,
                    Id = result.Id,
                    SourceAnbarId = result.SourceAnbarId,
                    DestinationAnbarId = result.DestinationAnbarId,
                    ItemCount = result.ItemCount,
                    Message = $"سند انتقال با موفقیت ویرایش شد. سند جدید: {result.Id}."
                },
                "سند انتقال با موفقیت ویرایش شد.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ApiResponse<StockTransferResponse>> DeleteAsync(
        int idSal,
        string id,
        CancellationToken ct)
    {
        if (idSal <= 0 || string.IsNullOrWhiteSpace(id))
            throw new ApiException(400, "INVALID_TRANSFER", "شناسه سند انتقال معتبر نیست.");

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var original = await LoadActiveTransferAsync(idSal, id, ct);
            var reverse = await ReverseAndDisableTransferAsync(
                original.Source, original.Destination, original.Items, ct);

            await transaction.CommitAsync(ct);
            return ApiResponse<StockTransferResponse>.SuccessResult(
                new StockTransferResponse
                {
                    IdSal = idSal,
                    Id = id,
                    SourceAnbarId = original.Source.IdAnbar,
                    DestinationAnbarId = original.Destination.IdAnbar,
                    ItemCount = original.Items.Count,
                    Message = $"سند حذف شد؛ انتقال معکوس با سند {reverse.Id} ثبت شد."
                },
                "سند انتقال با موفقیت حذف شد و موجودی آن به‌صورت معکوس برگشت.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
    public async Task<ApiResponse<StockTransferResponse>> CreateAsync(
        StockTransferRequest request,
        CancellationToken ct)
    {
        if (request.IdSal <= 0)
            throw new ApiException(400, "INVALID_SAL", "سال مالی معتبر نیست.");
        if (request.SourceAnbarId <= 0 || request.DestinationAnbarId <= 0 ||
            request.SourceAnbarId == request.DestinationAnbarId)
            throw new ApiException(400, "INVALID_WAREHOUSE", "انبار مبدأ و مقصد را به‌درستی انتخاب کنید.");
        if (string.IsNullOrWhiteSpace(request.SabtDate) || request.SabtDate.Length != 10)
            throw new ApiException(400, "INVALID_DATE", "تاریخ سند باید به صورت yyyy/MM/dd باشد.");

        var items = NormalizeTransferItems(request.Items);

        if (items.Count == 0)
            throw new ApiException(400, "EMPTY_TRANSFER", "حداقل یک کالا برای انتقال انتخاب کنید.");

        var warehouses = await _context.Anbars.AsNoTracking()
            .Where(x => x.Id == request.SourceAnbarId || x.Id == request.DestinationAnbarId)
            .ToListAsync(ct);

        if (warehouses.Count != 2)
            throw new ApiException(404, "WAREHOUSE_NOT_FOUND", "انبار مبدأ یا مقصد پیدا نشد.");

        var productIds = items.Select(x => x.IdKala).ToList();
        var products = await _context.Kalas.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var sourceStocks = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var destinationStocks = new Dictionary<string, decimal>(StringComparer.Ordinal);

        // The native transfer procedure deliberately uses the legacy neutral taraf
        // reference (IDTaraf=0, IDTarafType=2) and the table defaults for CheckDef
        // (IDSandogh=1, IDSandoghType=1). Validate the native reference row and
        // provide a real user id for the document owner.
        var neutralTarafExists = await _context.Tarafs.AsNoTracking()
            .AnyAsync(x => x.Id == 0 && x.IdType == 2, ct);

        var neutralUserId = await _context.Users.AsNoTracking()
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (!neutralTarafExists || !neutralUserId.HasValue)
            throw new ApiException(
                409,
                "TRANSFER_REFERENCE_DATA_MISSING",
                "رکوردهای پایه لازم برای ثبت سند انتقال موجود نیستند.");

        var nativeProcedureExists = await _context.Database
            .SqlQueryRaw<int>("SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.InsertTwoSanadRelated') AND type = 'P') THEN 1 ELSE 0 END AS [Value]")
            .SingleAsync(ct);

        if (nativeProcedureExists != 1)
            throw new ApiException(
                409,
                "TRANSFER_PROCEDURE_MISSING",
                "Procedure اصلی انتقال انبار (InsertTwoSanadRelated) در دیتابیس پیدا نشد.");

        var defaultCheckDefExists = await _context.CheckDefs.AsNoTracking()
            .AnyAsync(x => x.Id == 1 && x.Type == 1, ct);

        if (!defaultCheckDefExists)
            throw new ApiException(
                409,
                "TRANSFER_CHECKDEF_MISSING",
                "حساب پیش‌فرض CheckDef با شناسه 1 و نوع 1 در دیتابیس وجود ندارد؛ ثبت سند انتقال در ساختار اصلی KianStore ممکن نیست.");

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.IdKala, out var product) || product.IsDisabled)
                throw new ApiException(404, "PRODUCT_NOT_FOUND", $"کالا با کد {item.IdKala} پیدا نشد.");

            var sourceStock = await CalculateStockFromDocumentsAsync(
                item.IdKala, request.SourceAnbarId, request.IdSal, ct);

            if (sourceStock < item.Quantity)
                throw new ApiException(
                    409,
                    "INSUFFICIENT_STOCK",
                    $"موجودی «{product.KalaName}» در انبار مبدأ کافی نیست. موجودی: {sourceStock}، درخواست: {item.Quantity}.");

            sourceStocks[item.IdKala] = sourceStock;
            destinationStocks[item.IdKala] = await CalculateStockFromDocumentsAsync(
                item.IdKala, request.DestinationAnbarId, request.IdSal, ct);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            // Re-check inside the transaction so two simultaneous transfers cannot overspend stock.
            foreach (var item in items)
            {
                var currentStock = await CalculateStockFromDocumentsAsync(
                    item.IdKala, request.SourceAnbarId, request.IdSal, ct);

                if (currentStock < item.Quantity)
                    throw new ApiException(
                        409,
                        "INSUFFICIENT_STOCK",
                        $"موجودی کالای {item.IdKala} هنگام ثبت انتقال تغییر کرده است.");
            }

            var sanadId = await GenerateSanadIdAsync(request.IdSal, ct);
            // Use the legacy/native transfer mechanism: two linked Sanad headers
            // (type 6 = source/out, type 7 = destination/in). This is the same mechanism
            // used by the existing database procedures and keeps all legacy constraints intact.
            var secondSanadId = GetRelatedSanadId(sanadId);

            await InsertNativeTransferHeadersAsync(
                request.IdSal,
                sanadId,
                secondSanadId,
                request.SourceAnbarId,
                request.DestinationAnbarId,
                request.SabtDate,
                neutralUserId.Value,
                request.Note,
                description: null,
                ct);

            var nativeHeaders = await _context.Sanads.AsNoTracking()
                .Where(x => x.IdSal == request.IdSal && (x.Id == sanadId || x.Id == secondSanadId))
                .Select(x => new { x.Id, x.SanadType, x.IdAnbar })
                .ToListAsync(ct);

            var sourceHeader = nativeHeaders.FirstOrDefault(x => x.Id == sanadId);
            var destinationHeader = nativeHeaders.FirstOrDefault(x => x.Id == secondSanadId);

            if (sourceHeader == null || destinationHeader == null ||
                sourceHeader.SanadType != SourceTransferType ||
                destinationHeader.SanadType != DestinationTransferType ||
                sourceHeader.IdAnbar != request.SourceAnbarId ||
                destinationHeader.IdAnbar != request.DestinationAnbarId)
            {
                throw new ApiException(
                    409,
                    "TRANSFER_NATIVE_HEADER_INVALID",
                    "دستور انتقال داخلی دیتابیس سندهای نوع 6 و 7 را مطابق انبارهای مبدأ و مقصد ایجاد نکرد.");
            }

            var details = new List<SanadDetail>();
            var row = 1;

            foreach (var item in items)
            {
                var product = products[item.IdKala];
                var qty = (double)item.Quantity;

                // Type 6 / first linked sanad: stock leaves the source warehouse.
                details.Add(CreateDetail(
                    request.IdSal, sanadId, row++, product, request.SourceAnbarId,
                    bed: 0, bes: qty, sanadType: SourceTransferType));

                // Type 7 / second linked sanad: the same stock enters the destination warehouse.
                details.Add(CreateDetail(
                    request.IdSal, secondSanadId, row++, product, request.DestinationAnbarId,
                    bed: qty, bes: 0, sanadType: DestinationTransferType));
            }

            // Insert SanadDetail with an explicit SQL column list. This avoids EF
            // metadata issues in legacy KianStore databases and guarantees that no
            // SQL timestamp/rowversion column is ever included in the INSERT.
            await InsertTransferDetailsAsync(details, ct);

            // Keep the optional stock cache synchronized. This also uses explicit SQL
            // and never writes the timestamp/rowversion column.
            foreach (var item in items)
            {
                await SetCachedStockAsync(
                    item.IdKala,
                    request.SourceAnbarId,
                    sourceStocks[item.IdKala] - item.Quantity,
                    products[item.IdKala],
                    ct);

                await SetCachedStockAsync(
                    item.IdKala,
                    request.DestinationAnbarId,
                    destinationStocks[item.IdKala] + item.Quantity,
                    products[item.IdKala],
                    ct);
            }

            await transaction.CommitAsync(ct);

            return ApiResponse<StockTransferResponse>.SuccessResult(
                new StockTransferResponse
                {
                    IdSal = request.IdSal,
                    Id = sanadId,
                    SourceAnbarId = request.SourceAnbarId,
                    DestinationAnbarId = request.DestinationAnbarId,
                    ItemCount = items.Count,
                    Message = $"انتقال موجودی با موفقیت ثبت شد. سند مبدأ: {sanadId}، سند مقصد: {secondSanadId}."
                },
                "انتقال موجودی با موفقیت ثبت شد.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task InsertTransferDetailsAsync(
        IReadOnlyList<SanadDetail> details,
        CancellationToken ct)
    {
        const string sql = """
INSERT INTO dbo.SanadDetail
(
    IDSal, IDSanad, ID2, AtfNum, IDKala, Bed, Bes, BedMab, BesMab, Des, SumMab,
    IDAnbar, IDKalaType, BedMabKharid, Maliat, Maliat1, Maliat2, TakhfifDarsad,
    PorsantDarsad, HazKala, HazKalaKharid, IDSanjesh, IDSanjesh2, BedBesZarib,
    SanadType, PropKala, PropKala2, Des1, Des2, Des3, SumBed, SumBes, HazKala2,
    HazKala3, SumTakhfifKala, HazKala1, HazKalaGift1, HazKalaGift2, HazKalaGift3,
    IdAttribValuesStock, TakhfifD2, TakhfifD3, TakhfifMab1, TakhfifMab2,
    MaliatD1, MaliatD2, TasviehRoz, MaliatMab1, MaliatMab2, SumMabTakh,
    SumMabMaliat, MabFroshByTakh, Bed2, Bes2, BedMab2, BesMab2, MabEzafatMoaf
)
VALUES
(
    @IdSal, @IdSanad, @Id2, @AtfNum, @IdKala, @Bed, @Bes, @BedMab, @BesMab, @Des, @SumMab,
    @IdAnbar, @IdKalaType, @BedMabKharid, @Maliat, @Maliat1, @Maliat2, @TakhfifDarsad,
    @PorsantDarsad, @HazKala, @HazKalaKharid, @IdSanjesh, @IdSanjesh2, @BedBesZarib,
    @SanadType, @PropKala, @PropKala2, @Des1, @Des2, @Des3, @SumBed, @SumBes, @HazKala2,
    @HazKala3, @SumTakhfifKala, @HazKala1, @HazKalaGift1, @HazKalaGift2, @HazKalaGift3,
    @IdAttribValuesStock, @TakhfifD2, @TakhfifD3, @TakhfifMab1, @TakhfifMab2,
    @MaliatD1, @MaliatD2, @TasviehRoz, @MaliatMab1, @MaliatMab2, @SumMabTakh,
    @SumMabMaliat, @MabFroshByTakh, @Bed2, @Bes2, @BedMab2, @BesMab2, @MabEzafatMoaf
);
""";

        foreach (var detail in details)
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            AddParameter(command, "@IdSal", DbType.Int32, detail.IdSal);
            AddParameter(command, "@IdSanad", DbType.AnsiString, detail.IdSanad);
            AddParameter(command, "@Id2", DbType.Int32, detail.Id2);
            AddParameter(command, "@AtfNum", DbType.AnsiString, (object?)detail.AtfNum ?? DBNull.Value);
            AddParameter(command, "@IdKala", DbType.AnsiString, detail.IdKala);
            AddParameter(command, "@Bed", DbType.Double, detail.Bed);
            AddParameter(command, "@Bes", DbType.Double, detail.Bes);
            AddParameter(command, "@BedMab", DbType.Decimal, detail.BedMab);
            AddParameter(command, "@BesMab", DbType.Decimal, detail.BesMab);
            AddParameter(command, "@Des", DbType.AnsiString, (object?)detail.Des ?? DBNull.Value);
            AddParameter(command, "@SumMab", DbType.Decimal, detail.SumMab);
            AddParameter(command, "@IdAnbar", DbType.Int32, detail.IdAnbar);
            AddParameter(command, "@IdKalaType", DbType.Decimal, detail.IdKalaType);
            AddParameter(command, "@BedMabKharid", DbType.Decimal, detail.BedMabKharid);
            AddParameter(command, "@Maliat", DbType.Decimal, detail.Maliat);
            AddParameter(command, "@Maliat1", DbType.Boolean, detail.Maliat1);
            AddParameter(command, "@Maliat2", DbType.Boolean, detail.Maliat2);
            AddParameter(command, "@TakhfifDarsad", DbType.Double, detail.TakhfifDarsad);
            AddParameter(command, "@PorsantDarsad", DbType.Single, detail.PorsantDarsad);
            AddParameter(command, "@HazKala", DbType.Decimal, detail.HazKala);
            AddParameter(command, "@HazKalaKharid", DbType.Decimal, detail.HazKalaKharid);
            AddParameter(command, "@IdSanjesh", DbType.Int32, detail.IdSanjesh);
            AddParameter(command, "@IdSanjesh2", DbType.Int32, detail.IdSanjesh2);
            AddParameter(command, "@BedBesZarib", DbType.Double, detail.BedBesZarib);
            AddParameter(command, "@SanadType", DbType.Int32, detail.SanadType);
            AddParameter(command, "@PropKala", DbType.Int32, (object?)detail.PropKala ?? DBNull.Value);
            AddParameter(command, "@PropKala2", DbType.Int32, (object?)detail.PropKala2 ?? DBNull.Value);
            AddParameter(command, "@Des1", DbType.AnsiString, (object?)detail.Des1 ?? DBNull.Value);
            AddParameter(command, "@Des2", DbType.AnsiString, (object?)detail.Des2 ?? DBNull.Value);
            AddParameter(command, "@Des3", DbType.AnsiString, (object?)detail.Des3 ?? DBNull.Value);
            AddParameter(command, "@SumBed", DbType.Double, (object?)detail.SumBed ?? DBNull.Value);
            AddParameter(command, "@SumBes", DbType.Double, (object?)detail.SumBes ?? DBNull.Value);
            AddParameter(command, "@HazKala2", DbType.Decimal, (object?)detail.HazKala2 ?? DBNull.Value);
            AddParameter(command, "@HazKala3", DbType.Decimal, (object?)detail.HazKala3 ?? DBNull.Value);
            AddParameter(command, "@SumTakhfifKala", DbType.Decimal, detail.SumTakhfifKala);
            AddParameter(command, "@HazKala1", DbType.Decimal, (object?)detail.HazKala1 ?? DBNull.Value);
            AddParameter(command, "@HazKalaGift1", DbType.Decimal, (object?)detail.HazKalaGift1 ?? DBNull.Value);
            AddParameter(command, "@HazKalaGift2", DbType.Decimal, (object?)detail.HazKalaGift2 ?? DBNull.Value);
            AddParameter(command, "@HazKalaGift3", DbType.Decimal, (object?)detail.HazKalaGift3 ?? DBNull.Value);
            AddParameter(command, "@IdAttribValuesStock", DbType.AnsiString, detail.IdAttribValuesStock);
            AddParameter(command, "@TakhfifD2", DbType.Double, (object?)detail.TakhfifD2 ?? DBNull.Value);
            AddParameter(command, "@TakhfifD3", DbType.Double, (object?)detail.TakhfifD3 ?? DBNull.Value);
            AddParameter(command, "@TakhfifMab1", DbType.Decimal, (object?)detail.TakhfifMab1 ?? DBNull.Value);
            AddParameter(command, "@TakhfifMab2", DbType.Decimal, (object?)detail.TakhfifMab2 ?? DBNull.Value);
            AddParameter(command, "@MaliatD1", DbType.Double, (object?)detail.MaliatD1 ?? DBNull.Value);
            AddParameter(command, "@MaliatD2", DbType.Double, (object?)detail.MaliatD2 ?? DBNull.Value);
            AddParameter(command, "@TasviehRoz", DbType.Int32, (object?)detail.TasviehRoz ?? DBNull.Value);
            AddParameter(command, "@MaliatMab1", DbType.Decimal, (object?)detail.MaliatMab1 ?? DBNull.Value);
            AddParameter(command, "@MaliatMab2", DbType.Decimal, (object?)detail.MaliatMab2 ?? DBNull.Value);
            AddParameter(command, "@SumMabTakh", DbType.Decimal, (object?)detail.SumMabTakh ?? DBNull.Value);
            AddParameter(command, "@SumMabMaliat", DbType.Decimal, (object?)detail.SumMabMaliat ?? DBNull.Value);
            AddParameter(command, "@MabFroshByTakh", DbType.Decimal, (object?)detail.MabFroshByTakh ?? DBNull.Value);
            AddParameter(command, "@Bed2", DbType.Double, detail.Bed2);
            AddParameter(command, "@Bes2", DbType.Double, detail.Bes2);
            AddParameter(command, "@BedMab2", DbType.Decimal, detail.BedMab2);
            AddParameter(command, "@BesMab2", DbType.Decimal, detail.BesMab2);
            AddParameter(command, "@MabEzafatMoaf", DbType.Decimal, (object?)detail.MabEzafatMoaf ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static SanadDetail CreateDetail(
        int idSal,
        string sanadId,
        int id2,
        Kala product,
        int idAnbar,
        double bed,
        double bes,
        int sanadType)
    {
        return new SanadDetail
        {
            IdSal = idSal, IdSanad = sanadId, Id2 = id2, AtfNum = null,
            IdKala = product.Id, Bed = bed, Bes = bes,
            BedMab = 0, BesMab = 0, Des = null, SumMab = 0,
            IdAnbar = idAnbar, IdKalaType = product.KalaType, BedMabKharid = 0,
            Maliat = 0, Maliat1 = false, Maliat2 = false, TakhfifDarsad = 0,
            PorsantDarsad = 0, HazKala = 0, HazKalaKharid = 0,
            IdSanjesh = product.IdSanjesh, IdSanjesh2 = product.IdSanjesh2,
            BedBesZarib = 1, SanadType = sanadType,
            PropKala = null, PropKala2 = null, Des1 = null, Des2 = null, Des3 = null,
            SumBed = null, SumBes = null, HazKala2 = null, HazKala3 = null,
            SumTakhfifKala = 0, HazKala1 = null, HazKalaGift1 = null,
            HazKalaGift2 = null, HazKalaGift3 = null, IdAttribValuesStock = string.Empty,
            TakhfifD2 = null, TakhfifD3 = null, TakhfifMab1 = null, TakhfifMab2 = null,
            MaliatD1 = null, MaliatD2 = null, TasviehRoz = null, MaliatMab1 = null,
            MaliatMab2 = null, SumMabTakh = null, SumMabMaliat = null,
            MabFroshByTakh = null,
            Bed2 = bed, Bes2 = bes, BedMab2 = 0, BesMab2 = 0, MabEzafatMoaf = null
        };
    }

    private static string GetRelatedSanadId(string sanadId)
    {
        if (!int.TryParse(sanadId, out var numericId))
            throw new ApiException(409, "INVALID_SANAD_ID", "شماره داخلی سند انتقال معتبر نیست.");

        return (numericId + 1).ToString().PadLeft(sanadId.Length, '0');
    }

    private async Task InsertNativeTransferHeadersAsync(
        int idSal,
        string sourceSanadId,
        string destinationSanadId,
        int sourceAnbarId,
        int destinationAnbarId,
        string sabtDate,
        int responsibleUserId,
        string? note,
        string? description,
        CancellationToken ct)
    {
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "dbo.InsertTwoSanadRelated";
        command.CommandType = CommandType.StoredProcedure;
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        AddParameter(command, "@IDSal", DbType.Int32, idSal);
        AddParameter(command, "@IDSanad", DbType.AnsiString, sourceSanadId);
        AddParameter(command, "@SanadType1", DbType.Int32, SourceTransferType);
        AddParameter(command, "@SanadType2", DbType.Int32, DestinationTransferType);
        AddParameter(command, "@IDAnbar1", DbType.Int32, sourceAnbarId);
        AddParameter(command, "@IDAnbar2", DbType.Int32, destinationAnbarId);
        AddParameter(command, "@IDTaraf", DbType.Int32, 0);
        AddParameter(command, "@IDTarafType", DbType.Int32, 2);
        AddParameter(command, "@SabtDate", DbType.AnsiString, sabtDate);
        AddParameter(command, "@IDMasool", DbType.Int32, responsibleUserId);
        var documentDescription = string.IsNullOrWhiteSpace(description)
            ? "انتقال موجودی بین انبارها"
            : description.Trim();
        AddParameter(command, "@Des1", DbType.AnsiString, documentDescription);
        AddParameter(command, "@Des2", DbType.AnsiString, documentDescription);
        AddParameter(command, "@TarafName2", DbType.AnsiString, string.Empty);
        AddParameter(command, "@Sharh1", DbType.AnsiString, note ?? string.Empty);
        AddParameter(command, "@Sharh2", DbType.AnsiString, note ?? string.Empty);
        AddParameter(command, "@Disable1", DbType.Boolean, false);
        AddParameter(command, "@Disable2", DbType.Boolean, false);
        AddParameter(command, "@IDSanadEx2", DbType.Int32, 0);
        AddParameter(command, "@IDSanadEx3", DbType.Int32, 0);
        AddParameter(command, "@ShowInSanad", DbType.Boolean, true);
        AddParameter(command, "@ShowInFaktor", DbType.Boolean, false);

        await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        DbType dbType,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<decimal> CalculateStockFromDocumentsAsync(
        string kalaId,
        int idAnbar,
        int idSal,
        CancellationToken ct)
    {
        var calculatedStock = await (
            from detail in _context.SanadDetails.AsNoTracking()
            join sanad in _context.Sanads.AsNoTracking()
                on new { detail.IdSal, Id = detail.IdSanad } equals new { sanad.IdSal, sanad.Id }
            where detail.IdSal == idSal
                  && detail.IdKala == kalaId
                  && detail.IdAnbar == idAnbar
                  && !sanad.Disable
                  && detail.SanadType != 16
                  && detail.SanadType != 19
            select (double?)(detail.Bed2 - detail.Bes2))
            .SumAsync(ct);

        return (decimal)(calculatedStock ?? 0d);
    }

    private async Task<string> GenerateSanadIdAsync(int idSal, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT dbo.GetNewIDAllSanadNewFunc(@IDSal);";
            command.CommandType = CommandType.Text;
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@IDSal";
            parameter.DbType = DbType.Int32;
            parameter.Value = idSal;
            command.Parameters.Add(parameter);

            var value = await command.ExecuteScalarAsync(ct);
            var id = value?.ToString();
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("سیستم نتوانست شماره داخلی سند انتقال را تولید کند.");

            return id;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private async Task<int> GetMarketIdAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT ISNULL(IDMarket, 0) FROM dbo.Inf WHERE ID = 1;";
            command.CommandType = CommandType.Text;
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var value = await command.ExecuteScalarAsync(ct);
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private async Task SetCachedStockAsync(
        string kalaId,
        int idAnbar,
        decimal quantity,
        Kala product,
        CancellationToken ct)
    {
        // KalaDetail.lastChanged is a SQL Server timestamp/rowversion column.
        // Never let EF generate an INSERT/UPDATE for that column during this
        // cache synchronization. Use explicit SQL column lists instead.
        var safeQuantity = Math.Max(0d, (double)quantity);

        var sql = """
IF EXISTS (
    SELECT 1
    FROM dbo.KalaDetail WITH (UPDLOCK, HOLDLOCK)
    WHERE IDKala = {0} AND IDAnbar = {1}
)
BEGIN
    UPDATE dbo.KalaDetail
    SET Quantity = {2}
    WHERE IDKala = {0} AND IDAnbar = {1};
END
ELSE
BEGIN
    INSERT INTO dbo.KalaDetail
        (IDKala, IDAnbar, Quantity, LastMabKharid, MabFrosh, MabFrosh1)
    VALUES
        ({0}, {1}, {2}, {3}, {4}, NULL);
END
""";

        await _context.Database.ExecuteSqlRawAsync(
            sql,
            kalaId,
            idAnbar,
            safeQuantity,
            product.MabKharid,
            product.MabFrosh);
    }
}
