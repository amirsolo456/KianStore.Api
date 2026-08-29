using KianStore.Api.Common;
using KianStore.Api.DTOs.Products;

namespace KianStore.Api.Services.Interfaces;

public interface IProductService
{
    Task<ApiResponse<IEnumerable<ProductResponse>>> GetAllProductsAsync();
    Task<ApiResponse<ProductResponse>> GetProductByIdAsync(string id);
}
