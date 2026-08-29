using System.ComponentModel.DataAnnotations;
using KianStore.Api.Models.KianStore;

namespace KianStore.Api.Models.Orders;

public enum MobileOrderStatus
{
    Created = 1,
    WaitingForPayment = 2,
    PaymentSubmitted = 3,
    PaymentVerified = 4,
    Confirmed = 5,
    Preparing = 6,
    Shipped = 7,
    Delivered = 8,
    Cancelled = 9,
    ConvertedToSanad = 10
}

public class MobileOrder
{
    public long Id { get; set; }

    [Required]
    [StringLength(20)]
    public string OrderNumber { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = null!;

    [Required]
    [StringLength(20)]
    public string Mobile { get; set; } = null!;

    public string? Address { get; set; }

    [StringLength(20)]
    public string? PaymentDate { get; set; }

    public decimal PaymentAmount { get; set; }

    public MobileOrderStatus Status { get; set; }

    public int? TarafId { get; set; }
    public int? TarafType { get; set; }

    [StringLength(20)]
    public string? SanadId { get; set; }
    public int? SanadSal { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public ICollection<MobileOrderItem> Items { get; set; } = new List<MobileOrderItem>();
    public ICollection<MobileOrderPayment> Payments { get; set; } = new List<MobileOrderPayment>();
}

public class MobileOrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    [Required]
    [StringLength(20)]
    public string KalaId { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public MobileOrder Order { get; set; } = null!;
}

public class MobileOrderPayment
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    [Required]
    [StringLength(20)]
    public string PaymentDate { get; set; } = null!;

    public decimal Amount { get; set; }

    [StringLength(50)]
    public string? TrackingNumber { get; set; }

    [StringLength(50)]
    public string? BankName { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public MobileOrder Order { get; set; } = null!;
}
