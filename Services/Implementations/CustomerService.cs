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
        if (request.PersonType is not (1 or 2))
        {
            return ApiResponse<CustomerResponse>.ErrorResult(
                "INVALID_PERSON_TYPE",
                "نوع شخص معتبر نیست.");
        }

        var mobile = request.Mobile?.Trim();
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return ApiResponse<CustomerResponse>.ErrorResult(
                "INVALID_MOBILE",
                "شماره موبایل الزامی است.");
        }

        if (await _customerRepository.ExistsByMobileAsync(mobile))
        {
            return ApiResponse<CustomerResponse>.ErrorResult(
                "DUPLICATE_MOBILE",
                "این شماره موبایل قبلاً ثبت شده است.");
        }

        var name = request.PersonType == 2
            ? request.CompanyName?.Trim()
            : $"{request.FirstName?.Trim()} {request.LastName?.Trim()}".Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return ApiResponse<CustomerResponse>.ErrorResult(
                "INVALID_NAME",
                request.PersonType == 2
                    ? "نام شرکت/فروشگاه الزامی است."
                    : "نام و نام خانوادگی الزامی است.");
        }

        var nextId = await _customerRepository.GetNextIdAsync();

        var taraf = new Taraf
        {
            Id = nextId,
            IdType = request.PersonType,
            Name = name,
            Mobile = mobile,
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
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
