using System.Data;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Models;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KianStore.Api.Services.Implementations;

public sealed class DocumentService : IDocumentService
{
    private readonly KianStoreDbContext _context;
    private readonly IStockService _stockService;

    public DocumentService(KianStoreDbContext context, IStockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    public async Task<ApiResponse<DocumentResponse>> CreateAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0) throw new ApiException(400, "EMPTY_DOCUMENT", "سند حداقل باید یک قلم داشته باشد.");
        if (request.SabtDate.Length != 10) throw new ApiException(400, "INVALID_DATE", "تاریخ سند باید به صورت yyyy/MM/dd باشد.");

        var tarafExists = await _context.Tarafs.AsNoTracking().AnyAsync(x => x.Id == request.IdTaraf && x.IdType == request.IdTarafType && !x.IsDisabled, cancellationToken);
        if (!tarafExists) throw new ApiException(404, "CUSTOMER_NOT_FOUND", "طرف حساب مورد نظر یافت نشد.");

        if (request.SanadType == 11 && request.PurchaseEmployeeId.HasValue)
        {
            var userExists = await _context.Users.AsNoTracking().AnyAsync(
                x => x.Id == request.PurchaseEmployeeId.Value && x.IdAnbar > 0,
                cancellationToken);
            if (!userExists) throw new ApiException(404, "PURCHASE_EMPLOYEE_NOT_FOUND", "خریدار داخلی مورد نظر یافت نشد یا انبار اختصاصی ندارد.");
        }

        if (!request.IsPending)
        {
            var cashboxExists = await _context.CheckDefs.AsNoTracking().AnyAsync(x => x.Id == request.IdSandogh && x.Type == request.IdSandoghType, cancellationToken);
            if (!cashboxExists) throw new ApiException(409, "INVALID_CASHBOX", "صندوق/حساب انتخاب‌شده معتبر نیست.");
        }

        var distinctKalaIds = request.Items.Select(x => x.IdKala?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
        var products = await _context.Kalas.AsNoTracking().Where(x => distinctKalaIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var stockWarnings = new List<object>();
        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0) throw new ApiException(400, "INVALID_QUANTITY", $"تعداد کالای {item.IdKala} باید بیشتر از صفر باشد.");
            if (!products.TryGetValue(item.IdKala, out var product)) throw new ApiException(404, "PRODUCT_NOT_FOUND", $"کالا با کد {item.IdKala} یافت نشد.");
            if (product.IsDisabled) throw new ApiException(409, "PRODUCT_DISABLED", $"کالای {item.IdKala} غیرفعال است.");
            if (!item.IsIncoming && request.CheckStock)
            {
                var stockWarehouseId = item.IdAnbar ?? request.IdAnbar;
                var stock = await _stockService.CheckAsync(item.IdKala, item.Quantity, stockWarehouseId, request.IdSal, cancellationToken);
                if (!stock.IsAvailable) stockWarnings.Add(new { code = "INSUFFICIENT_STOCK", message = $"موجودی کالای {item.IdKala} کافی نبود.", stock.KalaId, stock.IdAnbar, stock.IdSal, stock.Requested, stock.Available, stock.IsAvailable });
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var sanadId = await GenerateSanadIdAsync(request.IdSal, cancellationToken);
            var factorId = request.IdFaktor ?? await GenerateFactorIdAsync(request.IdSal, request.SanadType, cancellationToken);
            var purchaseResponsibleUserId = request.SanadType == 11 && request.PurchaseEmployeeId.HasValue
                ? request.PurchaseEmployeeId.Value
                : request.IdMasool;
            var purchaseAnbarId = request.IdAnbar;
            if (request.SanadType == 11 && request.PurchaseEmployeeId.HasValue)
            {
                purchaseAnbarId = await _context.Users.AsNoTracking()
                    .Where(x => x.Id == request.PurchaseEmployeeId.Value)
                    .Select(x => x.IdAnbar)
                    .FirstAsync(cancellationToken);
            }
            var sanad = new Sanad
            {
                IdSal = request.IdSal, Id = sanadId, SanadType = request.SanadType, IdAnbar = purchaseAnbarId,
                IdTaraf = request.IdTaraf, IdTarafType = request.IdTarafType, IdFaktor = factorId, IdTypeMab = 0,
                Takhfif = 0, MabDarSad = 0, MabKol = 0, MabNaghd = 0, MabFrosh = 0, SabtDate = request.SabtDate,
                MabCheck = 0, MabBed = 0, IdMasool = purchaseResponsibleUserId, IdTaiid = null, Des = request.Des,
                IDEijad = null, IdDoreh = null, CountGhest = 0, DarsadGhest = 0, Maliat1 = 0, Maliat1Darsad = 0,
                Maliat1Sel = false, Maliat2 = 0, Maliat2Darsad = 0, Maliat2Sel = false, MabHarGhest = 0,
                MabKolAghsat = 0, GhestSel = false, KarmozdFrosh = 0, TarafName2 = null, Sharh = request.Sharh,
                Takhfif2 = 0, IsTasvieh = false, TasviehID = 0, Disable = false, IDSanadEx = 0, IDSanadEx2 = 0,
                IDSanadEx3 = 0, ShowInSanad = true, ShowInFaktor = true, TasviehDate = request.SabtDate,
                IsTasviehDate = false, IDSandogh = request.IdSandogh, IDSandoghType = request.IdSandoghType,
                MabKart = 0, MabFish = 0, IDKart = 0, IDTypeKart = 0, IsFinal = !request.IsPending, IDFroshMabType = 0,
                SanadTime = DateTime.Now.ToString("HH:mm:ss"), TakhfifKala1 = false, TakhfifKala2 = false,
                TakhfifKala3 = false, IsMaliat1Darsad = false, IsMaliat1Kala = false, IsMaliat2Darsad = false,
                IsMaliat2Kala = false, IsPorsant = false, IsPorsantMabKol = false, IsPorsantMabKala = false,
                HazFaktor = 0, IDHazFaktor = 0, HazFaktor2 = 0, IDHazFaktor2 = 0, TakhfifDarsad = 0,
                TakhfifOnvan = null, MabEzaf = 0, MabEzafDarsad = 0, MabEzafOnvan = null, SefareshID = request.SefareshID,
                IsSavedFinal = !request.IsPending, IDSanad = 0, Takhfif3 = 0, TakhfifKala = 0, IDFish = 0, IDFoodMahal = 0,
                Tel = null, Add = null, CodeMeli = null, Miz = null, GpsLat = 0, GpsLong = 0, TasvieType = 0,
                TasvieCheck = 0, IDAnbar2 = purchaseAnbarId, IDTaraf2 = request.IdTaraf, HMarketID = await GetMarketIdAsync(cancellationToken),
                IDRef = string.Empty, SanadTypeRef = 0, IDRefRecive = string.Empty, FroshArzesh = null, TakhfifKalaArzesh = null,
                HMaliat1 = null, HMaliat2 = null, TejaratCode = null, StateMaliat = 0, SabtDateOrg = request.SabtDate,
                MabBonKart = 0, MabBonKartTakhfif = 0, Takhfif1 = 0, IDTarafTahator = request.IdTaraf, TasviehRozSum = null,
                IDSanadAtf = null, MabFroshCalNaghd = null, MabCalNaghd = null, MabKarMozd = null, MabTahator = null,
                SumMabEzafatMoaf = null, IDState = null, CodeMaliat = null, IsTasviehFaktor = null, TasviehMab = null
            };

            var details = new List<SanadDetail>();
            decimal total = 0;
            decimal totalDiscount = 0;
            var row = 1;
            foreach (var item in request.Items)
            {
                var product = products[item.IdKala];
                var unitPrice = item.UnitPrice ?? product.MabFrosh;
                var purchaseUnitPrice = request.IsPending ? 0 : item.PurchasePrice ?? product.MabKharid;
                var grossLineTotal = unitPrice * item.Quantity;
                var lineDiscount = Math.Clamp(item.Discount, 0m, grossLineTotal);
                var itemWarehouseId = item.IdAnbar ?? request.IdAnbar;
                var lineTotal = grossLineTotal - lineDiscount;
                total += lineTotal;
                totalDiscount += lineDiscount;
                details.Add(new SanadDetail
                {
                    IdSal = request.IdSal, IdSanad = sanadId, Id2 = row++, AtfNum = null, IdKala = product.Id,
                    Bed = item.IsIncoming ? (double)item.Quantity : 0, Bes = item.IsIncoming ? 0 : (double)item.Quantity,
                    BedMab = item.IsIncoming ? unitPrice : 0, BesMab = item.IsIncoming ? 0 : unitPrice, Des = item.Description,
                    SumMab = lineTotal, SumMabTakh = lineDiscount, SumTakhfifKala = lineDiscount, IdAnbar = itemWarehouseId, IdKalaType = product.KalaType, BedMabKharid = purchaseUnitPrice,
                    Maliat = 0, Maliat1 = false, Maliat2 = false, TakhfifDarsad = 0, PorsantDarsad = 0, HazKala = 0,
                    HazKalaKharid = 0, IdSanjesh = product.IdSanjesh, IdSanjesh2 = product.IdSanjesh2, BedBesZarib = 1,
                    SanadType = request.SanadType, PropKala = null, PropKala2 = null, Des1 = null, Des2 = null, Des3 = null,
                    SumBed = null, SumBes = null, HazKala2 = null, HazKala3 = null, HazKala1 = null,
                    HazKalaGift1 = null, HazKalaGift2 = null, HazKalaGift3 = null, IdAttribValuesStock = string.Empty,
                    TakhfifD2 = null, TakhfifD3 = null, TakhfifMab1 = null, TakhfifMab2 = null, MaliatD1 = null, MaliatD2 = null,
                    TasviehRoz = null, MaliatMab1 = null, MaliatMab2 = null, SumMabMaliat = null,
                    MabFroshByTakh = null, Bed2 = item.IsIncoming ? (double)item.Quantity : 0, Bes2 = item.IsIncoming ? 0 : (double)item.Quantity,
                    BedMab2 = item.IsIncoming ? unitPrice : 0, BesMab2 = item.IsIncoming ? 0 : unitPrice, MabEzafatMoaf = null
                });
            }

            sanad.Takhfif = totalDiscount;
            sanad.MabKol = total;
            sanad.MabFrosh = total;
            sanad.MabNaghd = 0;
            sanad.MabBed = total;
            _context.Sanads.Add(sanad); _context.SanadDetails.AddRange(details);
            await _context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            var response = Map(sanad, details);
            return stockWarnings.Count > 0 ? ApiResponse<DocumentResponse>.SuccessWithWarningResult(response, stockWarnings, "سند ثبت شد، اما موجودی یک یا چند کالا کافی نبود.", "STOCK_WARNING") : ApiResponse<DocumentResponse>.SuccessResult(response, "سند با موفقیت ثبت شد.");
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    public async Task<ApiResponse<DocumentResponse>> GetAsync(int idSal, string id, CancellationToken cancellationToken = default)
    {
        var sanad = await _context.Sanads.AsNoTracking().FirstOrDefaultAsync(x => x.IdSal == idSal && x.Id == id, cancellationToken);
        if (sanad == null) throw new ApiException(404, "DOCUMENT_NOT_FOUND", "سند مورد نظر یافت نشد.");
        var details = await _context.SanadDetails.AsNoTracking().Where(x => x.IdSal == idSal && x.IdSanad == id).OrderBy(x => x.Id2).ToListAsync(cancellationToken);
        var tarafName = await _context.Tarafs.AsNoTracking().Where(x => x.Id == sanad.IdTaraf && x.IdType == sanad.IdTarafType).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        var purchaseEmployeeId = sanad.SanadType == 11 ? sanad.IdMasool : (int?)null;
        var purchaseEmployeeName = purchaseEmployeeId.HasValue
            ? await _context.Users.AsNoTracking().Where(x => x.Id == purchaseEmployeeId.Value).Select(x => x.UserFLName).FirstOrDefaultAsync(cancellationToken)
            : null;
        return ApiResponse<DocumentResponse>.SuccessResult(Map(sanad, details, tarafName, purchaseEmployeeName));
    }

    public async Task<ApiResponse<IReadOnlyList<DocumentResponse>>> GetHistoryAsync(int idSal, int sanadType = 12, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var sanadsQuery = _context.Sanads.AsNoTracking().Where(x => x.SanadType == sanadType && !x.Disable);
        if (idSal > 0) sanadsQuery = sanadsQuery.Where(x => x.IdSal == idSal);
        var sanads = await sanadsQuery.OrderByDescending(x => x.IdSal).ThenByDescending(x => x.IdFaktor).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        if (sanads.Count == 0) return ApiResponse<IReadOnlyList<DocumentResponse>>.SuccessResult(Array.Empty<DocumentResponse>(), "تاریخچه فروش خالی است.");
        var sanadIds = sanads.Select(x => x.Id).ToList();
        var salIds = sanads.Select(x => x.IdSal).Distinct().ToList();
        var details = await _context.SanadDetails.AsNoTracking().Where(x => sanadIds.Contains(x.IdSanad) && salIds.Contains(x.IdSal)).OrderBy(x => x.IdSal).ThenBy(x => x.IdSanad).ThenBy(x => x.Id2).ToListAsync(cancellationToken);
        var tarafIds = sanads.Select(x => x.IdTaraf).Distinct().ToList();
        var tarafs = await _context.Tarafs.AsNoTracking().Where(x => tarafIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var purchaseEmployeeIds = sanads.Where(x => x.SanadType == 11).Select(x => x.IdMasool).Distinct().ToList();
        var purchaseUsers = purchaseEmployeeIds.Count == 0
            ? new List<Users>()
            : await _context.Users.AsNoTracking().Where(x => purchaseEmployeeIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var detailLookup = details.ToLookup(x => x.IdSal + "|" + x.IdSanad);
        var result = sanads.Select(s => Map(
            s,
            detailLookup[s.IdSal + "|" + s.Id].ToList(),
            tarafs.FirstOrDefault(t => t.Id == s.IdTaraf && t.IdType == s.IdTarafType)?.Name,
            s.SanadType == 11 ? purchaseUsers.FirstOrDefault(x => x.Id == s.IdMasool)?.UserFLName : null)).ToList();
        return ApiResponse<IReadOnlyList<DocumentResponse>>.SuccessResult(result, "تاریخچه فروش با موفقیت دریافت شد.");
    }

    private async Task<string> GenerateSanadIdAsync(int idSal, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open; if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT dbo.GetNewIDAllSanadNewFunc(@IDSal);"; command.CommandType = CommandType.Text; command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var parameter = command.CreateParameter(); parameter.ParameterName = "@IDSal"; parameter.DbType = DbType.Int32; parameter.Value = idSal; command.Parameters.Add(parameter);
            var value = await command.ExecuteScalarAsync(cancellationToken); var id = value?.ToString(); if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("سیستم نتوانست شماره داخلی سند را تولید کند."); return id;
        }
        finally { if (shouldClose) await connection.CloseAsync(); }
    }

    private async Task<int> GenerateFactorIdAsync(int idSal, int sanadType, CancellationToken cancellationToken)
        => (await _context.Sanads.Where(x => x.IdSal == idSal && x.SanadType == sanadType).Select(x => (int?)x.IdFaktor).MaxAsync(cancellationToken) ?? 0) + 1;

    private async Task<int> GetMarketIdAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection(); var shouldClose = connection.State != ConnectionState.Open; if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT ISNULL(IDMarket, 0) FROM dbo.Inf WHERE ID = 1;"; command.CommandType = CommandType.Text; command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var value = await command.ExecuteScalarAsync(cancellationToken); return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }
        finally { if (shouldClose) await connection.CloseAsync(); }
    }

    private static DocumentResponse Map(Sanad sanad, IReadOnlyCollection<SanadDetail> details, string? tarafName = null, string? purchaseEmployeeName = null)
    {
        var isPurchase = sanad.SanadType == 11;
        // Always expose a stable SMS status to the document-history API.
        var smsStatus = sanad.SmsStatus?.Trim().ToLowerInvariant() switch
        {
            OrderRegistrationSmsServiceV2.SuccessStatus => OrderRegistrationSmsServiceV2.SuccessStatus,
            OrderRegistrationSmsServiceV2.FailedStatus => OrderRegistrationSmsServiceV2.FailedStatus,
            _ => "not_sent"
        };
        return new DocumentResponse
        {
            IdSal = sanad.IdSal, Id = sanad.Id, SanadType = sanad.SanadType, IdAnbar = sanad.IdAnbar, IdTaraf = sanad.IdTaraf,
            IdTarafType = sanad.IdTarafType, IdFaktor = sanad.IdFaktor, SabtDate = sanad.SabtDate, TotalAmount = sanad.MabKol,
            DiscountAmount = sanad.Takhfif,
            IsFinal = sanad.IsFinal, Description = sanad.Des, TarafName = tarafName,
            PurchaseEmployeeId = isPurchase ? sanad.IdMasool : null,
            PurchaseEmployeeName = isPurchase ? purchaseEmployeeName : null,
            SmsStatus = smsStatus,
            Items = details.Select(x => new DocumentItemResponse
            {
                Id2 = x.Id2,
                IdKala = x.IdKala,
                Quantity = isPurchase ? (x.Bed2 > 0 ? x.Bed2 : x.Bed) : (x.Bes2 > 0 ? x.Bes2 : x.Bes),
                UnitPrice = isPurchase ? (x.BedMab2 > 0 ? x.BedMab2 : x.BedMab) : (x.BesMab2 > 0 ? x.BesMab2 : x.BesMab),
                TotalAmount = x.SumMab,
                PurchasePrice = x.BedMabKharid
            }).ToList()
        };
    }
}
