using KianStore.Api.Models.Orders;

namespace KianStore.Api.Repositories.Interfaces;

public interface IOrderRepository
{
    Task<MobileOrder?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<MobileOrder>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        MobileOrderStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<MobileOrder> CreateAsync(
        MobileOrder order,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        MobileOrder order,
        CancellationToken cancellationToken = default);

    Task<string> GenerateOrderNumberAsync(
        CancellationToken cancellationToken = default);
}
