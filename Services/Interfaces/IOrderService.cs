using KianStore.Api.Common;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Models.Orders;

namespace KianStore.Api.Services.Interfaces;

public interface IOrderService
{
    Task<ApiResponse<OrderResponse>> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> GetOrderByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IEnumerable<OrderResponse>>> GetOrdersAsync(
        int page = 1,
        int pageSize = 20,
        MobileOrderStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> AddPaymentAsync(
        long orderId,
        AddPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> VerifyPaymentAsync(
        long orderId,
        long paymentId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> ConfirmOrderAsync(
        long orderId,
        CancellationToken cancellationToken = default);
}
