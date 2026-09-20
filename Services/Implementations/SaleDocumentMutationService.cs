using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Documents;
using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Services.Implementations;

// Mobile sale edit/delete service; included explicitly in the API deployment build.
public sealed class SaleDocumentMutationService
{
    private const int SaleType = 12;
    private readonly KianStoreDbContext _context;
    private readonly IDocumentService _documents;

    public SaleDocumentMutationService(KianStoreDbContext context, IDocumentService documents)
    {
        _context = context;
        _documents = documents;
    }

    public async Task<ApiResponse<DocumentResponse>> UpdateAsync(int idSal, string id, UpdateSaleDocumentRequest request, int? userId, CancellationToken ct = default)
    {
        if (request.Items.Count == 0) throw new ApiException(400, "EMPTY_DOCUMENT", "سند حداقل باید یک قلم داشته باشد.");
        var sanad = await _context.Sanads.FirstOrDefaultAsync(x => x.IdSal == idSal && x.Id == id && x.SanadType == SaleType && !x.Disable, ct);
        if (sanad == null) throw new ApiException(404, "SALE_NOT_FOUND", "سند فروش مورد نظر یافت نشد.");

        var ids = request.Items.Select(x => x.IdKala.Trim()).Where(x => x.Length > 0).Distinct().ToList();
        var products = await _context.Kalas.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0) throw new ApiException(400, "INVALID_QUANTITY", "تعداد کالا باید بیشتر از صفر باشد.");
            if (!products.TryGetValue(item.IdKala.Trim(), out var product)) throw new ApiException(404, "PRODUCT_NOT_FOUND", $"کالا با کد {item.IdKala} یافت نشد.");
            if (product.IsDisabled) throw new ApiException(409, "PRODUCT_DISABLED", $"کالای {item.IdKala} غیرفعال است.");
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            _context.SanadDetails.RemoveRange(_context.SanadDetails.Where(x => x.IdSal == idSal && x.IdSanad == id));
            var details = new List<SanadDetail>(request.Items.Count);
            decimal total = 0;
            var row = 1;
            foreach (var item in request.Items)
            {
                var product = products[item.IdKala.Trim()];
                var lineTotal = item.UnitPrice * item.Quantity;
                total += lineTotal;
                details.Add(new SanadDetail
                {
                    IdSal = idSal, IdSanad = id, Id2 = row++, IdKala = product.Id,
                    Bed = 0, Bes = (double)item.Quantity, BedMab = 0, BesMab = item.UnitPrice,
                    Des = item.Description, SumMab = lineTotal, IdAnbar = sanad.IdAnbar,
                    IdKalaType = product.KalaType, BedMabKharid = item.PurchasePrice,
                    Maliat = 0, Maliat1 = false, Maliat2 = false, TakhfifDarsad = 0, PorsantDarsad = 0,
                    HazKala = 0, HazKalaKharid = 0, IdSanjesh = product.IdSanjesh, IdSanjesh2 = product.IdSanjesh2,
                    BedBesZarib = 1, SanadType = SaleType, IdAttribValuesStock = string.Empty,
                    SumTakhfifKala = 0, Bed2 = 0, Bes2 = (double)item.Quantity, BedMab2 = 0, BesMab2 = item.UnitPrice
                });
            }
            sanad.MabKol = total; sanad.MabFrosh = total; sanad.MabBed = total;
            _context.SanadDetails.AddRange(details);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var result = await _documents.GetAsync(idSal, id, ct);
            if (!result.Success || result.Data == null) return ApiResponse<DocumentResponse>.ErrorResult("DOCUMENT_RESPONSE_LOAD_FAILED", "سند ویرایش شد اما اطلاعات نهایی قابل بازیابی نیست.");
            return ApiResponse<DocumentResponse>.SuccessResult(result.Data, "سند فروش با موفقیت ویرایش شد.");
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<ApiResponse<DocumentResponse>> DeleteAsync(int idSal, string id, int? userId, string? password, CancellationToken ct = default)
    {
        var sanad = await _context.Sanads.FirstOrDefaultAsync(x => x.IdSal == idSal && x.Id == id && x.SanadType == SaleType && !x.Disable, ct);
        if (sanad == null) throw new ApiException(404, "SALE_NOT_FOUND", "سند فروش مورد نظر یافت نشد.");

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            sanad.Disable = true;
            sanad.IsFinal = false;
            sanad.IsSavedFinal = false;
            sanad.ShowInSanad = false;
            sanad.ShowInFaktor = false;
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ApiResponse<DocumentResponse>.SuccessResult(
                new DocumentResponse
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
                    TarafName = null,
                    SmsStatus = sanad.SmsStatus,
                    Items = new List<DocumentItemResponse>()
                },
                "سند فروش با موفقیت حذف شد.");
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }
}
