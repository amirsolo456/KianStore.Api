using KianStore.Api.Common;
using KianStore.Api.DTOs.Customers;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<ApiResponse<CustomerResponse>> GetByMobileAsync(string mobile)
    {
        var taraf = await _customerRepository.GetByMobileAsync(mobile);
        if (taraf == null)
        {
            return ApiResponse<CustomerResponse>.ErrorResult(
                "CUSTOMER_NOT_FOUND",
                "مشتری با این شماره موبایل یافت نشد.");
        }

        return ApiResponse<CustomerResponse>.SuccessResult(Map(taraf));
    }

    public async Task<ApiResponse<IEnumerable<CustomerResponse>>> SearchAsync(
        string search,
        int page = 1,
        int pageSize = 50)
    {
        var tarafs = await _customerRepository.SearchAsync(search, page, pageSize);
        var response = tarafs.Select(Map).ToList();
        return ApiResponse<IEnumerable<CustomerResponse>>.SuccessResult(response);
    }

    public async Task<ApiResponse<CustomerResponse>> CreateCustomerAsync(CreateCustomerRequest request)
    {
        if (await _customerRepository.ExistsByMobileAsync(request.Mobile))
        {
            return ApiResponse<CustomerResponse>.ErrorResult(
                "DUPLICATE_MOBILE",
                "این شماره موبایل قبلاً ثبت شده است.");
        }

        var nextId = await _customerRepository.GetNextIdAsync();

        var taraf = new Taraf
        {
            Id = nextId,
            IdType = 1,
            Name = $"{request.FirstName} {request.LastName}".Trim(),
            Mobile = request.Mobile,
            Address = request.Address,
            IsBuyer = true,
            IsDisabled = false
        };

        await _customerRepository.CreateAsync(taraf);

        return ApiResponse<CustomerResponse>.SuccessResult(
            Map(taraf),
            "مشتری با موفقیت ایجاد شد.");
    }

    private static CustomerResponse Map(Taraf taraf) => new()
    {
        Id = taraf.Id,
        IdType = taraf.IdType,
        Name = taraf.Name,
        Address = taraf.Address,
        Mobile = taraf.Mobile,
        Phone = taraf.Phone
    };
}
