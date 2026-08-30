using KianStore.Api.Models.KianStore;

namespace KianStore.Api.Repositories.Interfaces;

public interface ICustomerRepository
{
    Task<Taraf?> GetByMobileAsync(string mobile);
    Task<Taraf?> GetByIdAsync(int id);
    Task<List<Taraf>> SearchAsync(string search, int page = 1, int pageSize = 50);
    Task<Taraf> CreateAsync(Taraf taraf);
    Task<bool> ExistsByMobileAsync(string mobile);
    Task<int> GetNextIdAsync();
}
