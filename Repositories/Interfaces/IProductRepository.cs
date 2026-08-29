using KianStore.Api.Models.KianStore;

namespace KianStore.Api.Repositories.Interfaces;

public interface IProductRepository
{
    Task<Kala?> GetByIdAsync(string id);
    Task<IEnumerable<Kala>> GetAllAsync(int take = 100);
}
