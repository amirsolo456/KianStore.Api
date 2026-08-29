using KianStore.Api.Models.Orders;

namespace KianStore.Api.Repositories.Interfaces;

public interface IOrderRepository
{
    Task<MobileOrder?> GetByIdAsync(long id);
    Task<IEnumerable<MobileOrder>> GetAllAsync(int page = 1, int pageSize = 20, MobileOrderStatus? status = null);
    Task<MobileOrder> CreateAsync(MobileOrder order);
    Task UpdateAsync(MobileOrder order);
    Task<string> GenerateOrderNumberAsync();
}
