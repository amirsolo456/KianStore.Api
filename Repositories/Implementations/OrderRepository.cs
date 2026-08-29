using Microsoft.EntityFrameworkCore;
using KianStore.Api.Data;
using KianStore.Api.Models.Orders;
using KianStore.Api.Repositories.Interfaces;

namespace KianStore.Api.Repositories.Implementations;

public sealed class OrderRepository : IOrderRepository
{
    private readonly KianStoreDbContext _context;

    public OrderRepository(KianStoreDbContext context)
    {
        _context = context;
    }

    public async Task<MobileOrder?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _context.MobileOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(
                o => o.Id == id,
                cancellationToken);
    }

    public async Task<IEnumerable<MobileOrder>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        MobileOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MobileOrders
            .AsNoTracking()
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<MobileOrder> CreateAsync(
        MobileOrder order,
        CancellationToken cancellationToken = default)
    {
        _context.MobileOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task UpdateAsync(
        MobileOrder order,
        CancellationToken cancellationToken = default)
    {
        _context.MobileOrders.Update(order);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GenerateOrderNumberAsync(
        CancellationToken cancellationToken = default)
    {
        var date = DateTime.Now.ToString("yyyyMMdd");

        var count = await _context.MobileOrders
            .CountAsync(
                o => o.CreatedAt.Date == DateTime.Today,
                cancellationToken);

        return $"MO-{date}-{(count + 1):D4}";
    }
}
