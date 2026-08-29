namespace KianStore.Api.DTOs.Customers;

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Mobile { get; set; } = null!;
    public string? Address { get; set; }
}
