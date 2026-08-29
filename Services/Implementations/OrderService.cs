using KianStore.Api.Common;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Models.Orders;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Services.Implementations;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IStockService _stockService;

    private const int CurrentSal = 1405;
    private const int DefaultAnbarId = 1;

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

    public async Task<ApiResponse<OrderResponse>> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return ApiResponse<OrderResponse>.ErrorResult("INVALID_REQUEST", "اطلاعات سفارش معتبر نیست.");

        if (string.IsNullOrWhiteSpace(request.Mobile))
            return ApiResponse<OrderResponse>.ErrorResult("INVALID_MOBILE", "شماره موبایل الزامی است.");

        if (request.Items is null || request.Items.Count == 0)
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_ITEMS_EMPTY", "حداقل یک کالا باید به سفارش اضافه شود.");

        var taraf = await _customerRepository.GetByMobileAsync(request.Mobile.Trim());
        int? tarafId = taraf?.Id;
        int? tarafType = taraf?.IdType;

        var items = new List<MobileOrderItem>();

        foreach (var itemReq in request.Items)
        {
            if (string.IsNullOrWhiteSpace(itemReq.KalaId))
                return ApiResponse<OrderResponse>.ErrorResult("INVALID_PRODUCT_ID", "شناسه کالا معتبر نیست.");

            if (itemReq.Quantity <= 0)
                return ApiResponse<OrderResponse>.ErrorResult("INVALID_QUANTITY", $"تعداد کالای {itemReq.KalaId} باید بیشتر از صفر باشد.");

            var product = await _productRepository.GetByIdAsync(itemReq.KalaId);

            if (product is null)
                return ApiResponse<OrderResponse>.ErrorResult("PRODUCT_NOT_FOUND", $"کالا با کد {itemReq.KalaId} یافت نشد.");

            if (product.IsDisabled)
                return ApiResponse<OrderResponse>.ErrorResult("PRODUCT_DISABLED", $"کالای «{product.KalaName}» قابل فروش نیست.");

            var canSell = await _stockService.CanSellAsync(
                product.Id,
                itemReq.Quantity,
                DefaultAnbarId,
                CurrentSal,
                cancellationToken);

            if (!canSell)
                return ApiResponse<OrderResponse>.ErrorResult(
                    "INSUFFICIENT_STOCK",
                    $"موجودی کالای «{product.KalaName}» برای تعداد درخواستی کافی نیست.");

            var unitPrice = product.MabFrosh;
            var totalPrice = itemReq.Quantity * unitPrice;

            items.Add(new MobileOrderItem
            {
                KalaId = product.Id,
                Quantity = itemReq.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice
            });
        }

        var order = new MobileOrder
        {
            OrderNumber = await _orderRepository.GenerateOrderNumberAsync(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Mobile = request.Mobile.Trim(),
            Address = request.Address?.Trim(),
            PaymentDate = request.PaymentDate,
            PaymentAmount = request.PaymentAmount ?? 0,
            Status = MobileOrderStatus.Created,
            TarafId = tarafId,
            TarafType = tarafType,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.Now,
            CreatedBy = 101,
            Items = items
        };

        await _orderRepository.CreateAsync(order);

        return await GetOrderByIdAsync(order.Id, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponse>> AddPaymentAsync(
        long orderId,
        AddPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");

        if (request is null)
            return ApiResponse<OrderResponse>.ErrorResult("INVALID_PAYMENT", "اطلاعات پرداخت معتبر نیست.");

        if (request.Amount <= 0)
            return ApiResponse<OrderResponse>.ErrorResult("INVALID_PAYMENT_AMOUNT", "مبلغ پرداخت باید بیشتر از صفر باشد.");

        if (order.Status == MobileOrderStatus.Cancelled || order.Status == MobileOrderStatus.ConvertedToSanad)
            return ApiResponse<OrderResponse>.ErrorResult("INVALID_ORDER_STATUS", "امکان ثبت پرداخت برای این وضعیت سفارش وجود ندارد.");

        order.Payments.Add(new MobileOrderPayment
        {
            OrderId = orderId,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            TrackingNumber = request.TrackingNumber?.Trim(),
            BankName = request.BankName?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.Now,
            CreatedBy = 101
        });

        order.PaymentAmount += request.Amount;

        if (order.Status is MobileOrderStatus.Created or MobileOrderStatus.WaitingForPayment)
            order.Status = MobileOrderStatus.PaymentSubmitted;

        await _orderRepository.UpdateAsync(order);
        return await GetOrderByIdAsync(orderId, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponse>> VerifyPaymentAsync(
        long orderId,
        long paymentId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");

        var payment = order.Payments.FirstOrDefault(p => p.Id == paymentId);

        if (payment is null)
            return ApiResponse<OrderResponse>.ErrorResult("PAYMENT_NOT_FOUND", "اطلاعات پرداخت یافت نشد.");

        if (order.Status == MobileOrderStatus.Cancelled)
            return ApiResponse<OrderResponse>.ErrorResult("INVALID_ORDER_STATUS", "سفارش لغو شده است.");

        order.Status = MobileOrderStatus.PaymentVerified;
        await _orderRepository.UpdateAsync(order);
        return await GetOrderByIdAsync(orderId, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponse>> ConfirmOrderAsync(
        long orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");

        if (order.Status != MobileOrderStatus.PaymentVerified &&
            order.Status != MobileOrderStatus.PaymentSubmitted)
        {
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_ORDER_STATUS",
                "سفارش باید در وضعیت پرداخت باشد تا تأیید شود.");
        }

        foreach (var item in order.Items)
        {
            var canSell = await _stockService.CanSellAsync(
                item.KalaId,
                item.Quantity,
                DefaultAnbarId,
                CurrentSal,
                cancellationToken);

            if (!canSell)
            {
                return ApiResponse<OrderResponse>.ErrorResult(
                    "INSUFFICIENT_STOCK",
                    $"موجودی کالای {item.KalaId} برای تعداد درخواستی کافی نیست.");
            }
        }

        order.Status = MobileOrderStatus.Confirmed;
        await _orderRepository.UpdateAsync(order);
        return await GetOrderByIdAsync(orderId, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponse>> GetOrderByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(id);

        if (order is null)
            return ApiResponse<OrderResponse>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");

        return ApiResponse<OrderResponse>.SuccessResult(MapToResponse(order));
    }

    public async Task<ApiResponse<IEnumerable<OrderResponse>>> GetOrdersAsync(
        int page = 1,
        int pageSize = 20,
        MobileOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var orders = await _orderRepository.GetAllAsync(page, pageSize, status);
        var response = orders.Select(MapToResponse).ToList();

        return ApiResponse<IEnumerable<OrderResponse>>.SuccessResult(response);
    }

    private static OrderResponse MapToResponse(MobileOrder order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            FirstName = order.FirstName,
            LastName = order.LastName,
            Mobile = order.Mobile,
            Address = order.Address,
            PaymentDate = order.PaymentDate,
            PaymentAmount = order.PaymentAmount,
            TotalAmount = order.Items.Sum(i => i.TotalPrice),
            Status = order.Status,
            TarafId = order.TarafId,
            SanadId = order.SanadId,
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
