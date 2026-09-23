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
    private const int TransferType = 114;

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

        var items = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.IdKala))
            .GroupBy(x => x.IdKala.Trim(), StringComparer.Ordinal)
            .Select(g => new StockTransferItemRequest
            {
                IdKala = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .Where(x => x.Quantity > 0)
            .ToList();

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

        // Sanad has legacy foreign keys to Taraf, CheckDef and Users even for
        // inventory-only documents. Resolve valid neutral records instead of
        // inserting zero values, which violate those foreign keys.
        var neutralTaraf = await _context.Tarafs.AsNoTracking()
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.IdType })
            .FirstOrDefaultAsync(ct);

        var neutralCheckDef = await _context.CheckDefs.AsNoTracking()
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Type })
            .FirstOrDefaultAsync(ct);

        var neutralUserId = await _context.Users.AsNoTracking()
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (neutralTaraf == null || neutralCheckDef == null || !neutralUserId.HasValue)
            throw new ApiException(
                409,
                "TRANSFER_REFERENCE_DATA_MISSING",
                "اطلاعات پایه لازم برای ثبت سند انتقال در دیتابیس موجود نیست.");

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
            var factorId = (await _context.Sanads
                .Where(x => x.IdSal == request.IdSal && x.SanadType == TransferType)
                .Select(x => (int?)x.IdFaktor)
                .MaxAsync(ct) ?? 0) + 1;

            var sanad = new Sanad
            {
                IdSal = request.IdSal,
                Id = sanadId,
                SanadType = TransferType,
                IdAnbar = request.SourceAnbarId,
                IdTaraf = neutralTaraf.Id,
                IdTarafType = neutralTaraf.IdType,
                IdFaktor = factorId,
                IdTypeMab = 0,
                Takhfif = 0, MabDarSad = 0, MabKol = 0, MabNaghd = 0, MabFrosh = 0,
                SabtDate = request.SabtDate,
                MabCheck = 0, MabBed = 0, IdMasool = neutralUserId.Value,
                IdTaiid = null, Des = "انتقال موجودی بین انبارها", IDEijad = null, IdDoreh = null,
                CountGhest = 0, DarsadGhest = 0,
                Maliat1 = 0, Maliat1Darsad = 0, Maliat1Sel = false,
                Maliat2 = 0, Maliat2Darsad = 0, Maliat2Sel = false,
                MabHarGhest = 0, MabKolAghsat = 0, GhestSel = false, KarmozdFrosh = 0,
                TarafName2 = null, Sharh = request.Note,
                Takhfif2 = 0, IsTasvieh = false, TasviehID = 0, Disable = false,
                IDSanadEx = 0, IDSanadEx2 = 0, IDSanadEx3 = 0,
                ShowInSanad = true, ShowInFaktor = false,
                TasviehDate = request.SabtDate, IsTasviehDate = false,
                IDSandogh = neutralCheckDef.Id, IDSandoghType = neutralCheckDef.Type,
                MabKart = 0, MabFish = 0, IDKart = 0, IDTypeKart = 0,
                IsFinal = true, IDFroshMabType = 0, SanadTime = DateTime.Now.ToString("HH:mm:ss"),
                TakhfifKala1 = false, TakhfifKala2 = false, TakhfifKala3 = false,
                IsMaliat1Darsad = false, IsMaliat1Kala = false,
                IsMaliat2Darsad = false, IsMaliat2Kala = false,
                IsPorsant = false, IsPorsantMabKol = false, IsPorsantMabKala = false,
                HazFaktor = 0, IDHazFaktor = 0, HazFaktor2 = 0, IDHazFaktor2 = 0,
                TakhfifDarsad = 0, TakhfifOnvan = null, MabEzaf = 0, MabEzafDarsad = 0,
                MabEzafOnvan = null, SefareshID = null, IsSavedFinal = true, IDSanad = 0,
                Takhfif3 = 0, TakhfifKala = 0, IDFish = 0, IDFoodMahal = 0,
                Tel = null, Add = null, CodeMeli = null, Miz = null, GpsLat = 0, GpsLong = 0,
                TasvieType = 0, TasvieCheck = 0,
                IDAnbar2 = request.DestinationAnbarId, IDTaraf2 = 0,
                HMarketID = await GetMarketIdAsync(ct),
                IDRef = string.Empty, SanadTypeRef = 0, IDRefRecive = string.Empty,
                FroshArzesh = null, TakhfifKalaArzesh = null, HMaliat1 = null, HMaliat2 = null,
                TejaratCode = null, StateMaliat = 0, SabtDateOrg = request.SabtDate,
                MabBonKart = 0, MabBonKartTakhfif = 0, Takhfif1 = 0, IDTarafTahator = 0,
                TasviehRozSum = null, IDSanadAtf = null, MabFroshCalNaghd = null,
                MabCalNaghd = null, MabKarMozd = null, MabTahator = null,
                SumMabEzafatMoaf = null, IDState = null, CodeMaliat = null,
                IsTasviehFaktor = null, TasviehMab = null
            };

            var details = new List<SanadDetail>();
            var row = 1;

            foreach (var item in items)
            {
                var product = products[item.IdKala];
                var qty = (double)item.Quantity;

                // Source row: stock leaves the source warehouse.
                details.Add(CreateDetail(
                    request.IdSal, sanadId, row++, product, request.SourceAnbarId,
                    bed: 0, bes: qty));

                // Destination row: the same stock enters the destination warehouse.
                details.Add(CreateDetail(
                    request.IdSal, sanadId, row++, product, request.DestinationAnbarId,
                    bed: qty, bes: 0));
            }

            _context.Sanads.Add(sanad);
            _context.SanadDetails.AddRange(details);
            await _context.SaveChangesAsync(ct);

            // Keep the optional stock cache synchronized when a row exists or can be created.
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

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ApiResponse<StockTransferResponse>.SuccessResult(
                new StockTransferResponse
                {
                    IdSal = request.IdSal,
                    Id = sanadId,
                    SourceAnbarId = request.SourceAnbarId,
                    DestinationAnbarId = request.DestinationAnbarId,
                    ItemCount = items.Count,
                    Message = "انتقال موجودی با موفقیت ثبت شد."
                },
                "انتقال موجودی با موفقیت ثبت شد.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static SanadDetail CreateDetail(
        int idSal,
        string sanadId,
        int id2,
        Kala product,
        int idAnbar,
        double bed,
        double bes)
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
            BedBesZarib = 1, SanadType = TransferType,
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
                  && detail.SanadType != 7
                  && detail.SanadType != 15
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
        var row = await _context.KalaDetails
            .FirstOrDefaultAsync(x => x.IdKala == kalaId && x.IdAnbar == idAnbar, ct);

        if (row == null)
        {
            _context.KalaDetails.Add(new KalaDetail
            {
                IdKala = kalaId,
                IdAnbar = idAnbar,
                Quantity = (double)Math.Max(0, quantity),
                LastMabKharid = product.MabKharid,
                MabFrosh = product.MabFrosh
            });
        }
        else
        {
            row.Quantity = (double)Math.Max(0, quantity);
        }
    }
}
