using KianStore.Api.Common;
using KianStore.Api.DTOs.Customers;

namespace KianStore.Api.Services.Interfaces;

public interface ICustomerService
{
    Task<ApiResponse<CustomerResponse>> GetByMobileAsync(string mobile);
    Task<ApiResponse<CustomerResponse>> CreateCustomerAsync(CreateCustomerRequest request);
}
