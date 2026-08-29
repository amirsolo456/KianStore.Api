namespace KianStore.Api.Repositories.Interfaces;

public interface IStockRepository
{
    Task<decimal> GetStockAsync(
        string kalaId,
        int idAnbar,
        int idSal,
        CancellationToken cancellationToken = default);
}
