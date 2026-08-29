using KianStore.Api.Data;
using KianStore.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Repositories.Implementations;

public sealed class StockRepository : IStockRepository
{
    private readonly KianStoreDbContext _context;

    public StockRepository(KianStoreDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetStockAsync(
        string kalaId,
        int idAnbar,
        int idSal,
        CancellationToken cancellationToken = default)
    {
        var cachedStock = await _context.KalaDetails
            .AsNoTracking()
            .Where(x => x.IdKala == kalaId && x.IdAnbar == idAnbar)
            .Select(x => (decimal?)x.Quantity)
            .FirstOrDefaultAsync(cancellationToken);

        if (cachedStock.HasValue)
            return cachedStock.Value;

        // KianStore recalculates stock from SanadDetail.Bed2 - Bes2.
        // Excluded document types are 16 and 19, matching the database logic.
        var calculatedStock = await _context.SanadDetails
            .AsNoTracking()
            .Where(x =>
                x.IdSal == idSal &&
                x.IdKala == kalaId &&
                x.IdAnbar == idAnbar &&
                x.SanadType != 16 &&
                x.SanadType != 19)
            .Select(x => (double?)(x.Bed2 - x.Bes2))
            .SumAsync(cancellationToken);

        return (decimal)(calculatedStock ?? 0d);
    }
}
