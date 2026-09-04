using KianStore.Api.Common;
using KianStore.Api.DTOs.Customers;

namespace KianStore.Api.Services.Interfaces;

public interface ICustomerService
{
    Task<ApiResponse<CustomerResponse>> GetByIdAsync(int id);
    Task<ApiResponse<CustomerResponse>> GetByMobileAsync(string mobile);
    Task<ApiResponse<IEnumerable<CustomerResponse>>> SearchAsync(string search, int page = 1, int pageSize = 50);
    Task<ApiResponse<CustomerResponse>> CreateCustomerAsync(CreateCustomerRequest request);
}
