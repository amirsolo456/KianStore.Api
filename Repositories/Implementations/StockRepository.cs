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
        var result = await _context.StoreAnbarMojodis
            .AsNoTracking()
            .Where(x =>
                x.IDSal == idSal &&
                x.IDAnbar == idAnbar &&
                x.IDKala == kalaId)
            .Select(x => (decimal?)x.Mojoodi)
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? 0m;
    }
}