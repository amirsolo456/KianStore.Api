using Microsoft.EntityFrameworkCore;
using KianStore.Api.Data;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Repositories.Interfaces;

namespace KianStore.Api.Repositories.Implementations;

public class ProductRepository : IProductRepository
{
    private readonly KianStoreDbContext _context;

    public ProductRepository(KianStoreDbContext context)
    {
        _context = context;
    }

    public async Task<Kala?> GetByIdAsync(string id)
    {
        return await _context.Kalas.FirstOrDefaultAsync(k => k.Id == id && !k.IsDisabled);
    }

    public async Task<IEnumerable<Kala>> GetAllAsync(int take = 100)
    {
        return await _context.Kalas
            .Where(k => !k.IsDisabled && k.Id != "" && k.Id != "00" && k.Id != "01")
            .Take(take)
            .ToListAsync();
    }
}
