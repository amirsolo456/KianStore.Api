using Microsoft.EntityFrameworkCore;
using KianStore.Api.Data;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Repositories.Interfaces;

namespace KianStore.Api.Repositories.Implementations;

public class CustomerRepository : ICustomerRepository
{
    private readonly KianStoreDbContext _context;

    public CustomerRepository(KianStoreDbContext context)
    {
        _context = context;
    }

    public async Task<Taraf?> GetByMobileAsync(string mobile)
    {
        return await _context.Tarafs
            .FirstOrDefaultAsync(t => (t.Mobile == mobile || t.Phone == mobile) && !t.IsDisabled);
    }

    public async Task<Taraf?> GetByIdAsync(int id)
    {
        return await _context.Tarafs.FindAsync(id);
    }

    public async Task<Taraf> CreateAsync(Taraf taraf)
    {
        _context.Tarafs.Add(taraf);
        await _context.SaveChangesAsync();
        return taraf;
    }

    public async Task<bool> ExistsByMobileAsync(string mobile)
    {
        return await _context.Tarafs.AnyAsync(t => t.Mobile == mobile || t.Phone == mobile);
    }

    public async Task<int> GetNextIdAsync()
    {
        // Note: For real KianStore, we should ideally use a stored procedure or a sequence if available.
        // For now, using Max + 1 within a transaction logic in service layer.
        var maxId = await _context.Tarafs.MaxAsync(t => (int?)t.Id) ?? 0;
        return maxId + 1;
    }
}
