namespace KianStore.Api.DTOs.Customers;

public class CreateCustomerRequest
{
    public int PersonType { get; set; } = 1;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string Mobile { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
}
