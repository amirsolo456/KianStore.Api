using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Services.Implementations;

public sealed class StockService : IStockService
{
    private readonly IStockRepository _repository;

    public StockService(IStockRepository repository)
    {
        _repository = repository;
    }

    public Task<decimal> GetStockAsync(
        string kalaId,
        int idAnbar,
        int idSal,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetStockAsync(
            kalaId,
            idAnbar,
            idSal,
            cancellationToken);
    }

    public async Task<bool> CanSellAsync(
        string kalaId,
        decimal quantity,
        int idAnbar,
        int idSal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kalaId))
            return false;

        if (quantity <= 0)
            return false;

        var stock = await GetStockAsync(
            kalaId,
            idAnbar,
            idSal,
            cancellationToken);

        return stock >= quantity;
    }
}