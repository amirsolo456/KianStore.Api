using KianStore.Api.Models.Orders;

namespace KianStore.Api.DTOs.Orders;

public class OrderResponse
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Mobile { get; set; } = null!;
    public string? Address { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public MobileOrderStatus Status { get; set; }
    public string StatusText => Status.ToString();
    public DateTime CreatedAt { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
}

public class OrderItemResponse
{
    public long Id { get; set; }
    public string KalaId { get; set; } = null!;
    public string KalaName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
