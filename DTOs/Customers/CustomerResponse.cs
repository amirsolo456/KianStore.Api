namespace KianStore.Api.DTOs.Customers;

public class CustomerResponse
{
    public int Id { get; set; }
    public int IdType { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
}
