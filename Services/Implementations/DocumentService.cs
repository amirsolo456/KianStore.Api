using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Models;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        if (request.Items.Count == 0)
            throw new ApiException(400, "EMPTY_DOCUMENT", "سند حداقل باید یک قلم داشته باشد.");

        if (string.IsNullOrWhiteSpace(request.SabtDate) || request.SabtDate.Length != 10)
            throw new ApiException(400, "INVALID_DATE", "تاریخ سند باید به صورت yyyy/MM/dd باشد.");

        var tarafExists = await _context.Tarafs.AsNoTracking()
            .AnyAsync(x => x.Id == request.IdTaraf && x.IdType == request.IdTarafType && !x.IsDisabled, cancellationToken);

        if (!tarafExists)
            throw new ApiException(404, "CUSTOMER_NOT_FOUND", "طرف حساب مورد نظر یافت نشد.");

        var kalaIds = request.Items.Select(x => x.IdKala).Distinct().ToList();
        var products = await _context.Kalas
            .Where(x => kalaIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ApiException(400, "INVALID_QUANTITY", $"تعداد کالای {item.IdKala} باید بیشتر از صفر باشد.");

            if (!products.TryGetValue(item.IdKala, out var product))
                throw new ApiException(404, "PRODUCT_NOT_FOUND", $"کالا با کد {item.IdKala} یافت نشد.");

            if (product.IsDisabled)
                throw new ApiException(409, "PRODUCT_DISABLED", $"کالای {item.IdKala} غیرفعال است.");

            if (!item.IsIncoming && request.CheckStock)
            {
                var stock = await _stockService.CheckAsync(item.IdKala, item.Quantity, request.IdAnbar, request.IdSal, cancellationToken);
                if (!stock.IsAvailable)
                {
                    throw new ApiException(409, "INSUFFICIENT_STOCK", $"موجودی کالای {item.IdKala} کافی نیست.", new
                    {
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

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        var sanadId = await GenerateSanadIdAsync(request.IdSal, cancellationToken);
        var factorId = request.IdFaktor ?? await GenerateFactorIdAsync(request.IdSal, cancellationToken);
        var sanad = new Sanad
        {
            IdSal = request.IdSal,
            Id = sanadId,
            SanadType = request.SanadType,
            IdAnbar = request.IdAnbar,
            IdTaraf = request.IdTaraf,
            IdTarafType = request.IdTarafType,
            IdFaktor = factorId,
            IdTypeMab = 0,
            Takhfif = 0,
            MabDarSad = 0,
            MabKol = 0,
            MabNaghd = 0,
            MabFrosh = 0,
            SabtDate = request.SabtDate,
            MabCheck = 0,
            MabBed = 0,
            IdMasool = request.IdMasool,
            Des = request.Des,
            CountGhest = 0,
            DarsadGhest = 0,
            Maliat1 = 0,
            Maliat1Darsad = 0,
            Maliat1Sel = false,
            Maliat2 = 0,
            Maliat2Darsad = 0,
            Maliat2Sel = false,
            MabHarGhest = 0,
            MabKolAghsat = 0,
            GhestSel = false,
            KarmozdFrosh = 0,
            Sharh = request.Sharh,
            Takhfif2 = 0,
            IsTasvieh = false,
            TasviehID = 0,
            Disable = false,
            IDSanadEx = 0,
            IDSanadEx2 = 0,
            IDSanadEx3 = 0,
            ShowInSanad = true,
            ShowInFaktor = true,
            TasviehDate = request.SabtDate,
            IsTasviehDate = false,
            IDSandogh = 0,
            IDSandoghType = 0,
            MabKart = 0,
            MabFish = 0,
            IDKart = 0,
            IDTypeKart = 0,
            IsFinal = false,
            IdFroshMabType = 0,
            SanadTime = DateTime.Now.ToString("HH:mm:ss"),
            TakhfifKala1 = false,
            TakhfifKala2 = false,
            TakhfifKala3 = false,
            IsMaliat1Darsad = false,
            IsMaliat1Kala = false,
            IsMaliat2Darsad = false,
            IsMaliat2Kala = false,
            IsPorsant = false,
            IsPorsantMabKol = false,
            IsPorsantMabKala = false,
            HazFaktor = 0,
            IDHazFaktor = 0,
            HazFaktor2 = 0,
            IDHazFaktor2 = 0,
            TakhfifDarsad = 0,
            MabEzaf = 0,
            MabEzafDarsad = 0,
            IsSavedFinal = false,
            Takhfif3 = 0,
            TakhfifKala = 0,
            IDFish = 0,
            IDFoodMahal = 0,
            GpsLat = 0,
            GpsLong = 0,
            TasvieType = 0,
            TasvieCheck = 0,
            IDAnbar2 = request.IdAnbar,
            IDTaraf2 = request.IdTaraf,
            HMarketID = 0,
            IDRef = "",
            SanadTypeRef = 0,
            IDRefRecive = "",
            StateMaliat = 0,
            SabtDateOrg = request.SabtDate,
            MabBonKart = 0,
            MabBonKartTakhfif = 0,
            Takhfif1 = 0,
            IDTarafTahator = 0
        };

        var details = new List<SanadDetail>();
        decimal total = 0;
        var row = 1;

        foreach (var item in request.Items)
        {
            var product = products[item.IdKala];
            var unitPrice = item.UnitPrice ?? product.MabFrosh;
            var lineTotal = unitPrice * item.Quantity;
            total += lineTotal;

            details.Add(new SanadDetail
            {
                IdSal = request.IdSal,
                IdSanad = sanadId,
                Id2 = row++,
                IdKala = product.Id,
                Bed = item.IsIncoming ? (double)item.Quantity : 0,
                Bes = item.IsIncoming ? 0 : (double)item.Quantity,
                BedMab = item.IsIncoming ? unitPrice : 0,
                BesMab = item.IsIncoming ? 0 : unitPrice,
                Des = item.Description,
                SumMab = lineTotal,
                IdAnbar = request.IdAnbar,
                IdKalaType = product.KalaType,
                BedMabKharid = product.MabKharid,
                IdSanjesh = product.IdSanjesh,
                IdSanjesh2 = product.IdSanjesh2,
                BedBesZarib = 1,
                SanadType = request.SanadType,
                SumTakhfifKala = 0,
                IdAttribValuesStock = "",
                Bed2 = item.IsIncoming ? (double)item.Quantity : 0,
                Bes2 = item.IsIncoming ? 0 : (double)item.Quantity,
                BedMab2 = item.IsIncoming ? unitPrice : 0,
                BesMab2 = item.IsIncoming ? 0 : unitPrice
            });
        }

        sanad.MabKol = total;
        sanad.MabFrosh = total;
        sanad.MabBed = total;

        _context.Sanads.Add(sanad);
        _context.SanadDetails.AddRange(details);
        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return ApiResponse<DocumentResponse>.SuccessResult(Map(sanad, details), "سند با موفقیت ثبت شد.");
    }

    public async Task<ApiResponse<DocumentResponse>> GetAsync(int idSal, string id, CancellationToken cancellationToken = default)
    {
        var sanad = await _context.Sanads.AsNoTracking().FirstOrDefaultAsync(x => x.IdSal == idSal && x.Id == id, cancellationToken);
        if (sanad == null)
            throw new ApiException(404, "DOCUMENT_NOT_FOUND", "سند مورد نظر یافت نشد.");

        var details = await _context.SanadDetails.AsNoTracking()
            .Where(x => x.IdSal == idSal && x.IdSanad == id)
            .OrderBy(x => x.Id2)
            .ToListAsync(cancellationToken);

        return ApiResponse<DocumentResponse>.SuccessResult(Map(sanad, details));
    }

    private async Task<string> GenerateSanadIdAsync(int idSal, CancellationToken cancellationToken)
    {
        var ids = await _context.Sanads.AsNoTracking().Where(x => x.IdSal == idSal).Select(x => x.Id).ToListAsync(cancellationToken);
        var max = ids.Select(x => int.TryParse(x, out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return (max + 1).ToString();
    }

    private async Task<int> GenerateFactorIdAsync(int idSal, CancellationToken cancellationToken)
    {
        return (await _context.Sanads.AsNoTracking().Where(x => x.IdSal == idSal).Select(x => (int?)x.IdFaktor).MaxAsync(cancellationToken) ?? 0) + 1;
    }

    private static DocumentResponse Map(Sanad sanad, IReadOnlyCollection<SanadDetail> details) => new()
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
        IsFinal = sanad.IsFinal,
        Description = sanad.Des,
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
