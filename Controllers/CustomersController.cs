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

    /// <summary>
    /// Returns the existing customer for a mobile number or creates the customer immediately.
    /// This is used by the website as the single entry point for customer identification.
    /// </summary>
    [HttpPost("ensure-by-mobile")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> EnsureByMobile(
        [FromBody] CreateCustomerRequest request)
    {
        var mobile = request.Mobile?.Trim();
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return BadRequest(ApiResponse<CustomerResponse>.ErrorResult(
                "INVALID_MOBILE",
                "شماره موبایل الزامی است."));
        }

        var existing = await _customerService.GetByMobileAsync(mobile);
        if (existing.Success && existing.Data != null)
            return Ok(existing);

        var created = await _customerService.CreateCustomerAsync(new CreateCustomerRequest
        {
            PersonType = request.PersonType,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            Mobile = mobile,
            Phone = request.Phone,
            Address = request.Address
        });

        if (!created.Success)
        {
            // A second simultaneous request may have created the customer between
            // the lookup and insert. Resolve that race by reading the customer again.
            var raceWinner = await _customerService.GetByMobileAsync(mobile);
            if (raceWinner.Success && raceWinner.Data != null)
                return Ok(raceWinner);

            return BadRequest(created);
        }

        return Ok(created);
    }
}
