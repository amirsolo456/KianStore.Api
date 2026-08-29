using KianStore.Api.Common;
using KianStore.Api.DTOs.Products;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Services.Implementations;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ApiResponse<IEnumerable<ProductResponse>>> GetAllProductsAsync()
    {
        var products = await _productRepository.GetAllAsync();
        var response = products.Select(p => new ProductResponse
        {
            Id = p.Id,
            Name = p.KalaName,
            Price = p.MabFrosh
        });

        return ApiResponse<IEnumerable<ProductResponse>>.SuccessResult(response);
    }

    public async Task<ApiResponse<ProductResponse>> GetProductByIdAsync(string id)
    {
        var product = await _productRepository.GetByIdAsync(id);

        if (product == null)
        {
            return ApiResponse<ProductResponse>.ErrorResult("PRODUCT_NOT_FOUND", "کالا یافت نشد.");
        }

        var response = new ProductResponse
        {
            Id = product.Id,
            Name = product.KalaName,
            Price = product.MabFrosh
        };

        return ApiResponse<ProductResponse>.SuccessResult(response);
    }
}
