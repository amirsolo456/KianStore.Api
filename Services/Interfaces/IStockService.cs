namespace KianStore.Api.Services.Interfaces;

public interface IStockService
{
    Task<decimal> GetStockAsync(
        string kalaId,
        int idAnbar,
        int idSal,
        CancellationToken cancellationToken = default);

    Task<StockCheckResult> CheckAsync(
        string kalaId,
        decimal quantity,
        int idAnbar,
        int idSal,
        CancellationToken cancellationToken = default);
}

public sealed record StockCheckResult(
    string KalaId,
    int IdAnbar,
    int IdSal,
    decimal Requested,
    decimal Available,
    bool IsAvailable);
