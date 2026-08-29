namespace KianStore.Api.Services.Interfaces;

public interface IStockService
{
    Task<decimal> GetStockAsync(
        string kalaId,
        int idAnbar,
        int idSal,
        CancellationToken cancellationToken = default);

    Task<bool> CanSellAsync(
        string kalaId,
        decimal quantity,
        int idAnbar,
        int idSal,
        CancellationToken cancellationToken = default);
}