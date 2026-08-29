using Microsoft.EntityFrameworkCore;
using KianStore.Api.Data;
using KianStore.Api.Models.Orders;
using KianStore.Api.Repositories.Interfaces;

namespace KianStore.Api.Repositories.Implementations;

public class OrderRepository : IOrderRepository
{
    private readonly KianStoreDbContext _context;

    public OrderRepository(KianStoreDbContext context)
    {
        _context = context;
    }

    public async Task<MobileOrder?> GetByIdAsync(long id)
    {
        return await _context.MobileOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<MobileOrder>> GetAllAsync(int page = 1, int pageSize = 20, MobileOrderStatus? status = null)
    {
        var query = _context.MobileOrders.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<MobileOrder> CreateAsync(MobileOrder order)
    {
        _context.MobileOrders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task UpdateAsync(MobileOrder order)
    {
        _context.MobileOrders.Update(order);
        await _context.SaveChangesAsync();
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        var count = await _context.MobileOrders.CountAsync(o => o.CreatedAt.Date == DateTime.Today);
        return $"MO-{date}-{(count + 1):D4}";
    }
}
