using KianStore.Api.Common;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Models.Orders;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Services.Implementations;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IStockService _stockService;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository,
        IStockService stockService)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _stockService = stockService;
    }

    public async Task<ApiResponse<OrderResponse>> CreateOrderAsync(CreateOrderRequest request)
    {
        // 1. Find or Create Customer
        var taraf = await _customerRepository.GetByMobileAsync(request.Mobile);
        int? tarafId = taraf?.Id;
        int? tarafType = taraf?.IdType;

        // 2. Calculate prices and build items
        var items = new List<MobileOrderItem>();
        decimal totalAmount = 0;

        foreach (var itemReq in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(itemReq.KalaId);
            if (product == null)
            {
                return ApiResponse<OrderResponse>.ErrorResult("PRODUCT_NOT_FOUND", $"کالا با کد {itemReq.KalaId} یافت نشد.");
            }

            var orderItem = new MobileOrderItem
            {
                KalaId = product.Id,
                Quantity = itemReq.Quantity,
                UnitPrice = product.MabFrosh,
                TotalPrice = itemReq.Quantity * product.MabFrosh
            };

            items.Add(orderItem);
            totalAmount += orderItem.TotalPrice;
        }

        // 3. Create Order
        var order = new MobileOrder
        {
            OrderNumber = await _orderRepository.GenerateOrderNumberAsync(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Mobile = request.Mobile,
            Address = request.Address,
            PaymentDate = request.PaymentDate,
            PaymentAmount = request.PaymentAmount ?? 0,
            Status = MobileOrderStatus.Created,
            TarafId = tarafId,
            TarafType = tarafType,
            Notes = request.Notes,
            CreatedAt = DateTime.Now,
            Items = items
        };

        await _orderRepository.CreateAsync(order);

        return await GetOrderByIdAsync(order.Id);
    }

    public async Task<ApiResponse<OrderResponse>> AddPaymentAsync(long orderId, AddPaymentRequest request)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");
        }

        if (order.Status == MobileOrderStatus.Cancelled || order.Status == MobileOrderStatus.ConvertedToSanad)
        {
            return ApiResponse<OrderResponse>.ErrorResult("INVALID_ORDER_STATUS", "امکان ثبت پرداخت برای این وضعیت سفارش وجود ندارد.");
        }

        var payment = new MobileOrderPayment
        {
            OrderId = orderId,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            TrackingNumber = request.TrackingNumber,
            BankName = request.BankName,
            Notes = request.Notes,
            CreatedAt = DateTime.Now
        };

        order.Payments.Add(payment);
        order.PaymentAmount += request.Amount;

        if (order.Status == MobileOrderStatus.Created || order.Status == MobileOrderStatus.WaitingForPayment)
        {
            order.Status = MobileOrderStatus.PaymentSubmitted;
        }

        await _orderRepository.UpdateAsync(order);

        return await GetOrderByIdAsync(orderId);
    }

    public async Task<ApiResponse<OrderResponse>> VerifyPaymentAsync(long orderId, long paymentId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");
        }

        var payment = order.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null)
        {
            return ApiResponse<OrderResponse>.ErrorResult("PAYMENT_NOT_FOUND", "اطلاعات پرداخت یافت نشد.");
        }

        // In a real scenario, this would involve checking with a bank gateway or manual verification
        // For now, we just mark it as verified.

        order.Status = MobileOrderStatus.PaymentVerified;
        await _orderRepository.UpdateAsync(order);

        return await GetOrderByIdAsync(orderId);
    }

    public async Task<ApiResponse<OrderResponse>> ConfirmOrderAsync(long orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");
        }

        if (order.Status != MobileOrderStatus.PaymentVerified && order.Status != MobileOrderStatus.PaymentSubmitted)
        {
            return ApiResponse<OrderResponse>.ErrorResult("INVALID_ORDER_STATUS", "سفارش باید در وضعیت پرداخت شده باشد تا تایید شود.");
        }

        // Check stock before confirmation
        foreach (var item in order.Items)
        {
            if (!await _stockService.CanSellAsync(
        item.KalaId,
        item.Quantity,
        item.idAnbar,
        item.idSal,
            cancellationToken))
            {
                return ApiResponse<OrderResponse>.ErrorResult("INSUFFICIENT_STOCK", $"موجودی کالای {item.KalaId} کافی نیست.");
            }
        }

        order.Status = MobileOrderStatus.Confirmed;
        await _orderRepository.UpdateAsync(order);

        return await GetOrderByIdAsync(orderId);
    }

    public async Task<ApiResponse<OrderResponse>> GetOrderByIdAsync(long id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null)
        {
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");
        }

        var response = MapToResponse(order);
        return ApiResponse<OrderResponse>.SuccessResult(response);
    }

    public async Task<ApiResponse<IEnumerable<OrderResponse>>> GetOrdersAsync(int page = 1, int pageSize = 20, MobileOrderStatus? status = null)
    {
        var orders = await _orderRepository.GetAllAsync(page, pageSize, status);
        var response = orders.Select(MapToResponse);
        return ApiResponse<IEnumerable<OrderResponse>>.SuccessResult(response);
    }

    private OrderResponse MapToResponse(MobileOrder order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            FirstName = order.FirstName,
            LastName = order.LastName,
            Mobile = order.Mobile,
            Address = order.Address,
            PaymentAmount = order.PaymentAmount,
            TotalAmount = order.Items.Sum(i => i.TotalPrice),
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => new OrderItemResponse
            {
                Id = i.Id,
                KalaId = i.KalaId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList()
        };
    }
}
