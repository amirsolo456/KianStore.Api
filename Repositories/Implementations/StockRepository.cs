using KianStore.Api.Data;
using KianStore.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Repositories.Implementations;

public sealed class StockRepository : IStockRepository
{
    private readonly KianStoreDbContext _context;

    public StockRepository(KianStoreDbContext context) { _context = context; }

    public async Task<decimal> GetStockAsync(string kalaId, int idAnbar, int idSal, CancellationToken cancellationToken = default)
    {
        var cachedStock = await _context.KalaDetails.AsNoTracking()
            .Where(x => x.IdKala == kalaId && x.IdAnbar == idAnbar)
            .Select(x => (decimal?)x.Quantity)
            .FirstOrDefaultAsync(cancellationToken);
        if (cachedStock.HasValue) return cachedStock.Value;

        var calculatedStock = await (
            from detail in _context.SanadDetails.AsNoTracking()
            join sanad in _context.Sanads.AsNoTracking()
                on new { detail.IdSal, Id = detail.IdSanad } equals new { sanad.IdSal, sanad.Id }
            where detail.IdSal == idSal && detail.IdKala == kalaId && detail.IdAnbar == idAnbar
                  && !sanad.Disable
                  && detail.SanadType != 7 && detail.SanadType != 15 && detail.SanadType != 16 && detail.SanadType != 19
            select (double?)(detail.Bed2 - detail.Bes2))
            .SumAsync(cancellationToken);
        return (decimal)(calculatedStock ?? 0d);
    }
}
