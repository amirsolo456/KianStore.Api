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
        mobile = mobile.Trim();
        return await _context.Tarafs
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                !t.IsDisabled &&
                (t.Mobile == mobile || t.Phone == mobile));
    }

    public async Task<Taraf?> GetByIdAsync(int id)
    {
        return await _context.Tarafs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDisabled);
    }

    public async Task<List<Taraf>> SearchAsync(string search, int page = 1, int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        search = search.Trim();

        var query = _context.Tarafs
            .AsNoTracking()
            .Where(t => !t.IsDisabled);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                t.Name.Contains(search) ||
                (t.Mobile != null && t.Mobile.Contains(search)) ||
                (t.Phone != null && t.Phone.Contains(search)) ||
                t.Id.ToString().Contains(search));
        }

        return await query
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Taraf> CreateAsync(Taraf taraf)
    {
        _context.Tarafs.Add(taraf);
        await _context.SaveChangesAsync();
        return taraf;
    }

    public async Task<bool> ExistsByMobileAsync(string mobile)
    {
        mobile = mobile.Trim();
        return await _context.Tarafs.AnyAsync(t =>
            t.Mobile == mobile || t.Phone == mobile);
    }

    public async Task<int> GetNextIdAsync()
    {
        var maxId = await _context.Tarafs.MaxAsync(t => (int?)t.Id) ?? 0;
        return maxId + 1;
    }
}
