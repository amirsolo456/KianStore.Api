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

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
    }

    public async Task<ApiResponse<OrderResponse>> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_REQUEST",
                "اطلاعات سفارش معتبر نیست.");

        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();
        var mobile = request.Mobile?.Trim();

        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName))
        {
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_CUSTOMER_NAME",
                "نام و نام خانوادگی الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(mobile))
        {
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_MOBILE",
                "شماره موبایل الزامی است.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return ApiResponse<OrderResponse>.ErrorResult(
                "ORDER_ITEMS_EMPTY",
                "حداقل یک کالا باید به سفارش اضافه شود.");
        }

        if (request.PaymentAmount.HasValue && request.PaymentAmount.Value < 0)
        {
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_PAYMENT_AMOUNT",
                "مبلغ پرداخت نمی‌تواند منفی باشد.");
        }

        if (request.PaymentAmount > 0 &&
            string.IsNullOrWhiteSpace(request.PaymentDate))
        {
            return ApiResponse<OrderResponse>.ErrorResult(
                "PAYMENT_DATE_REQUIRED",
                "برای مبلغ واریزی، تاریخ واریز الزامی است.");
        }

        var taraf = await _customerRepository.GetByMobileAsync(mobile);

        var items = new List<MobileOrderItem>(request.Items.Count);

        foreach (var itemRequest in request.Items)
        {
            var kalaId = itemRequest.KalaId?.Trim();

            if (string.IsNullOrWhiteSpace(kalaId))
            {
                return ApiResponse<OrderResponse>.ErrorResult(
                    "INVALID_PRODUCT_ID",
                    "شناسه کالا معتبر نیست.");
            }

            if (itemRequest.Quantity <= 0)
            {
                return ApiResponse<OrderResponse>.ErrorResult(
                    "INVALID_QUANTITY",
                    $"تعداد کالای {kalaId} باید بیشتر از صفر باشد.");
            }

            var product = await _productRepository.GetByIdAsync(kalaId);

            if (product is null)
            {
                return ApiResponse<OrderResponse>.ErrorResult(
                    "PRODUCT_NOT_FOUND",
                    $"کالا با کد {kalaId} یافت نشد.");
            }

            if (product.IsDisabled)
            {
                return ApiResponse<OrderResponse>.ErrorResult(
                    "PRODUCT_DISABLED",
                    $"کالای «{product.KalaName}» قابل فروش نیست.");
            }

            var unitPrice = product.MabFrosh;
            var totalPrice = itemRequest.Quantity * unitPrice;

            items.Add(new MobileOrderItem
            {
                KalaId = product.Id,
                Quantity = itemRequest.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice
            });
        }

        var paymentAmount = request.PaymentAmount ?? 0m;

        var order = new MobileOrder
        {
            OrderNumber = await _orderRepository.GenerateOrderNumberAsync(cancellationToken),
            FirstName = firstName,
            LastName = lastName,
            Mobile = mobile,
            Address = request.Address?.Trim(),
            PaymentDate = request.PaymentDate?.Trim(),
            PaymentAmount = paymentAmount,
            Status = paymentAmount > 0
                ? MobileOrderStatus.PaymentSubmitted
                : MobileOrderStatus.Created,
            TarafId = taraf?.Id,
            TarafType = taraf?.IdType,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.Now,
            CreatedBy = 101,
            Items = items
        };

        if (paymentAmount > 0)
        {
            order.Payments.Add(new MobileOrderPayment
            {
                PaymentDate = request.PaymentDate!.Trim(),
                Amount = paymentAmount,
                CreatedAt = DateTime.Now,
                CreatedBy = 101,
                Notes = "پرداخت ثبت‌شده هنگام ایجاد سفارش"
            });
        }

        await _orderRepository.CreateAsync(order, cancellationToken);

        return await GetOrderByIdAsync(order.Id, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponse>> AddPaymentAsync(
        long orderId,
        AddPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(
            orderId,
            cancellationToken);

        if (order is null)
            return ApiResponse<OrderResponse>.ErrorResult(
                "ORDER_NOT_FOUND",
                "سفارش یافت نشد.");

        if (request is null)
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_PAYMENT",
                "اطلاعات پرداخت معتبر نیست.");

        if (request.Amount <= 0)
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_PAYMENT_AMOUNT",
                "مبلغ پرداخت باید بیشتر از صفر باشد.");

        if (string.IsNullOrWhiteSpace(request.PaymentDate))
            return ApiResponse<OrderResponse>.ErrorResult(
                "PAYMENT_DATE_REQUIRED",
                "تاریخ پرداخت الزامی است.");

        if (order.Status is MobileOrderStatus.Cancelled or MobileOrderStatus.ConvertedToSanad)
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_ORDER_STATUS",
                "امکان ثبت پرداخت برای این وضعیت سفارش وجود ندارد.");

        order.Payments.Add(new MobileOrderPayment
        {
            OrderId = orderId,
            PaymentDate = request.PaymentDate.Trim(),
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

        await _orderRepository.UpdateAsync(order, cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponse>> VerifyPaymentAsync(
        long orderId,
        long paymentId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(
            orderId,
            cancellationToken);

        if (order is null)
            return ApiResponse<OrderResponse>.ErrorResult(
                "ORDER_NOT_FOUND",
                "سفارش یافت نشد.");

        var payment = order.Payments.FirstOrDefault(p => p.Id == paymentId);

        if (payment is null)
            return ApiResponse<OrderResponse>.ErrorResult(
                "PAYMENT_NOT_FOUND",
                "اطلاعات پرداخت یافت نشد.");

        if (order.Status == MobileOrderStatus.Cancelled)
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_ORDER_STATUS",
                "سفارش لغو شده است.");

        order.Status = MobileOrderStatus.PaymentVerified;

        await _orderRepository.UpdateAsync(order, cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponse>> ConfirmOrderAsync(
        long orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(
            orderId,
            cancellationToken);

        if (order is null)
            return ApiResponse<OrderResponse>.ErrorResult(
                "ORDER_NOT_FOUND",
                "سفارش یافت نشد.");

        if (order.Status is MobileOrderStatus.Cancelled or MobileOrderStatus.ConvertedToSanad)
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_ORDER_STATUS",
                "وضعیت فعلی سفارش اجازه تأیید را نمی‌دهد.");

        if (order.Status != MobileOrderStatus.PaymentVerified &&
            order.Status != MobileOrderStatus.PaymentSubmitted)
        {
            return ApiResponse<OrderResponse>.ErrorResult(
                "INVALID_ORDER_STATUS",
                "سفارش باید در وضعیت پرداخت باشد تا تأیید شود.");
        }

        // Stock validation is intentionally deferred until the actual KianStore
        // stock source is confirmed. The current StoreAnbarMojodi source has no
        // data after the database cleanup, so checking it here would incorrectly
        // reject valid orders.
        order.Status = MobileOrderStatus.Confirmed;

        await _orderRepository.UpdateAsync(order, cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken);
    }

    public async Task<ApiResponse<OrderResponse>> GetOrderByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (order is null)
            return ApiResponse<OrderResponse>.ErrorResult(
                "ORDER_NOT_FOUND",
                "سفارش یافت نشد.");

        return ApiResponse<OrderResponse>.SuccessResult(
            MapToResponse(order));
    }

    public async Task<ApiResponse<IEnumerable<OrderResponse>>> GetOrdersAsync(
        int page = 1,
        int pageSize = 20,
        MobileOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var orders = await _orderRepository.GetAllAsync(
            page,
            pageSize,
            status,
            cancellationToken);

        var response = orders
            .Select(MapToResponse)
            .ToList();

        return ApiResponse<IEnumerable<OrderResponse>>.SuccessResult(
            response);
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
