using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KianStore.Api.DTOs.Orders;

public sealed class WebPendingOrderFinalizeRequest
{
    [Range(1, int.MaxValue)] public int IdSal { get; init; } = 1405;
    [Range(1, int.MaxValue)] public int IdAnbar { get; init; } = 1;
    [Range(1, int.MaxValue)] public int IdMasool { get; init; } = 101;
    [Range(1, int.MaxValue)] public int IdSandogh { get; init; } = 1;
    [Range(0, int.MaxValue)] public int IdSandoghType { get; init; } = 1;
    [Range(1, int.MaxValue)] public int SanadType { get; init; } = 51;
    // The pending document already has a registration date. The mobile client
    // may omit it; the controller then keeps the date stored on the document.
    [StringLength(10)] public string? SabtDate { get; init; }
    [StringLength(200)] public string? Des { get; init; }
    [StringLength(700)] public string? Sharh { get; init; }
    public bool CheckStock { get; init; } = true;
    public List<WebPendingOrderItemFinalizeRequest> Items { get; init; } = new();
}

public sealed class WebPendingOrderItemFinalizeRequest
{
    private string? _kalaId;

    // Accept the normal camelCase kalaId as well as legacy clients that send
    // idKala. The controller continues to consume KalaId uniformly.
    [Required, StringLength(20)]
    public string? KalaId
    {
        get => string.IsNullOrWhiteSpace(_kalaId) ? IdKala : _kalaId;
        init => _kalaId = value;
    }

    [JsonPropertyName("idKala")]
    public string? IdKala { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal PurchasePrice { get; init; }
}
