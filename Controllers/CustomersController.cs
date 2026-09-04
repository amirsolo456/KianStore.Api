using Microsoft.AspNetCore.Mvc;
using KianStore.Api.Common;
using KianStore.Api.DTOs.Customers;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ICustomerRepository _customerRepository;

    public CustomersController(ICustomerService customerService, ICustomerRepository customerRepository)
    {
        _customerService = customerService;
        _customerRepository = customerRepository;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<CustomerResponse>>>> SearchCustomers(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _customerService.SearchAsync(search?.Trim() ?? string.Empty, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> GetCustomerById(int id)
    {
        var customer = await _customerRepository.GetByIdAsync(id);
        if (customer == null)
            return NotFound(ApiResponse<CustomerResponse>.ErrorResult("CUSTOMER_NOT_FOUND", "مشتری یافت نشد."));

        return Ok(ApiResponse<CustomerResponse>.SuccessResult(new CustomerResponse
        {
            Id = customer.Id,
            IdType = customer.IdType,
            Name = customer.Name,
            Address = customer.Address,
            Mobile = customer.Mobile,
            Phone = customer.Phone
        }));
    }

    [HttpGet("by-mobile/{mobile}")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> GetCustomerByMobile(string mobile)
    {
        var result = await _customerService.GetByMobileAsync(mobile);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> CreateCustomer(CreateCustomerRequest request)
    {
        var result = await _customerService.CreateCustomerAsync(request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
