using KianStore.Api.Common;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Models.Orders;

namespace KianStore.Api.Services.Interfaces;

public interface IOrderService
{
    Task<ApiResponse<OrderResponse>> CreateOrderAsync(CreateOrderRequest request);
    Task<ApiResponse<OrderResponse>> GetOrderByIdAsync(long id);
    Task<ApiResponse<IEnumerable<OrderResponse>>> GetOrdersAsync(int page = 1, int pageSize = 20, MobileOrderStatus? status = null);
    Task<ApiResponse<OrderResponse>> AddPaymentAsync(long orderId, AddPaymentRequest request);
    Task<ApiResponse<OrderResponse>> VerifyPaymentAsync(long orderId, long paymentId);
    Task<ApiResponse<OrderResponse>> ConfirmOrderAsync(long orderId);
}
