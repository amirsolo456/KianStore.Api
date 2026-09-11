using System.ComponentModel.DataAnnotations;

namespace KianStore.Api.DTOs.Orders;

public sealed class WebPendingOrderFinalizeRequest
{
    [Range(1, int.MaxValue)] public int IdSal { get; init; } = 1405;
    [Range(1, int.MaxValue)] public int IdAnbar { get; init; } = 1;
    [Range(1, int.MaxValue)] public int IdMasool { get; init; } = 101;
    [Range(1, int.MaxValue)] public int IdSandogh { get; init; } = 1;
    [Range(0, int.MaxValue)] public int IdSandoghType { get; init; } = 1;
    [Range(1, int.MaxValue)] public int SanadType { get; init; } = 12;
    [Required, StringLength(10)] public string SabtDate { get; init; } = null!;
    [StringLength(200)] public string? Des { get; init; }
    [StringLength(700)] public string? Sharh { get; init; }
    public bool CheckStock { get; init; } = true;
    [MinLength(1)] public List<WebPendingOrderItemFinalizeRequest> Items { get; init; } = new();
}

public sealed class WebPendingOrderItemFinalizeRequest
{
    [Required, StringLength(20)] public string KalaId { get; init; } = null!;
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal PurchasePrice { get; init; }
}
