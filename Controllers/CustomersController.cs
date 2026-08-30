using Microsoft.AspNetCore.Mvc;
using KianStore.Api.Common;
using KianStore.Api.DTOs.Customers;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
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

    [HttpGet("by-mobile/{mobile}")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> GetCustomerByMobile(string mobile)
    {
        var result = await _customerService.GetByMobileAsync(mobile);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> CreateCustomer(CreateCustomerRequest request)
    {
        var result = await _customerService.CreateCustomerAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
