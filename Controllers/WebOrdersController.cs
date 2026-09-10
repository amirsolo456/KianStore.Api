using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Orders;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/web-orders")]
public sealed class WebOrdersController : ControllerBase
{
    private readonly KianStoreDbContext _context;
    private readonly IStockService _stockService;

    public WebOrdersController(KianStoreDbContext context, IStockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] WebOrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "ORDER_ITEMS_REQUIRED",
                "حداقل یک کالا برای ثبت سفارش لازم است."));
        }

        if ((request.IdSal.HasValue && !request.IdAnbar.HasValue) ||
            (!request.IdSal.HasValue && request.IdAnbar.HasValue))
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "STOCK_CONTEXT_INCOMPLETE",
                "برای بررسی موجودی، سال مالی و انبار باید هر دو مشخص شوند."));
        }

        var requestedItems = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.KalaId) && x.Quantity > 0)
            .GroupBy(x => x.KalaId.Trim())
            .Select(g => new { KalaId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        if (requestedItems.Count == 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "ORDER_ITEMS_INVALID",
                "اقلام سفارش معتبر نیستند."));
        }

        var kalaIds = requestedItems.Select(x => x.KalaId).ToArray();
        var kalas = await _context.Kalas
            .AsNoTracking()
            .Where(x => kalaIds.Contains(x.Id) && !x.IsDisabled)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var missingKala = kalaIds.FirstOrDefault(id => !kalas.ContainsKey(id));
        if (missingKala is not null)
        {
            return NotFound(ApiResponse<object>.ErrorResult(
                "PRODUCT_NOT_FOUND",
                $"کالا با شناسه {missingKala} یافت نشد."));
        }

        if (request.TarafId.HasValue)
        {
            var tarafExists = await _context.Tarafs
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.TarafId.Value &&
                               (!request.TarafType.HasValue || x.IdType == request.TarafType.Value),
                    cancellationToken);

            if (!tarafExists)
            {
                return BadRequest(ApiResponse<object>.ErrorResult(
                    "CUSTOMER_NOT_FOUND",
                    "مشتری انتخاب‌شده یافت نشد."));
            }
        }

        if (request.IdSal.HasValue && request.IdAnbar.HasValue)
        {
            foreach (var item in requestedItems)
            {
                var stock = await _stockService.CheckAsync(
                    item.KalaId,
                    item.Quantity,
                    request.IdAnbar.Value,
                    request.IdSal.Value,
                    cancellationToken);

                if (!stock.IsAvailable)
                {
                    return Conflict(ApiResponse<object>.ErrorResult(
                        "INSUFFICIENT_STOCK",
                        $"موجودی کالای {item.KalaId} کافی نیست.",
                        new { stock.Available, stock.Requested, stock.KalaId }));
                }
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = DateTime.UtcNow;
            var order = new MobileOrder
            {
                OrderNumber = BuildOrderNumber(now),
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim(),
                Mobile = request.Mobile.Trim(),
                Address = request.Address?.Trim(),
                Status = 1,
                TarafId = request.TarafId,
                TarafType = request.TarafType,
                Notes = request.Notes?.Trim(),
                CreatedAt = now,
                CreatedBy = 0
            };

            foreach (var requested in requestedItems)
            {
                var kala = kalas[requested.KalaId];
                order.Items.Add(new MobileOrderItem
                {
                    KalaId = kala.Id,
                    Quantity = requested.Quantity,
                    UnitPrice = kala.MabFrosh,
                    TotalPrice = kala.MabFrosh * requested.Quantity
                });
            }

            await _context.MobileOrders.AddAsync(order, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var total = order.Items.Sum(x => x.TotalPrice);
            return Ok(ApiResponse<object>.SuccessResult(
                new
                {
                    order.Id,
                    order.OrderNumber,
                    order.Mobile,
                    order.Status,
                    TotalAmount = total,
                    Items = order.Items.Select(x => new
                    {
                        x.KalaId,
                        x.Quantity,
                        x.UnitPrice,
                        x.TotalPrice
                    })
                },
                "سفارش با موفقیت ثبت شد."));
        }
        catch
        {
            await RollbackSafelyAsync(transaction, cancellationToken);
            throw;
        }
    }

    [HttpGet("{orderNumber}")]
    public async Task<ActionResult<ApiResponse<object>>> Get(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var order = await _context.MobileOrders
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
        {
            return NotFound(ApiResponse<object>.ErrorResult(
                "ORDER_NOT_FOUND",
                "سفارش یافت نشد."));
        }

        var total = order.Items.Sum(x => x.TotalPrice);
        var paid = order.Payments.Sum(x => x.Amount);

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            order.Id,
            order.OrderNumber,
            order.FirstName,
            order.LastName,
            order.Mobile,
            order.Address,
            order.Status,
            order.PaymentDate,
            order.PaymentAmount,
            order.TarafId,
            order.TarafType,
            order.SanadId,
            order.SanadSal,
            order.Notes,
            order.CreatedAt,
            TotalAmount = total,
            PaidAmount = paid,
            RemainingAmount = Math.Max(0m, total - paid),
            Items = order.Items
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.KalaId,
                    x.Quantity,
                    x.UnitPrice,
                    x.TotalPrice
                }),
            Payments = order.Payments
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Amount,
                    x.PaymentDate,
                    x.TrackingNumber,
                    x.BankName,
                    x.Notes,
                    x.CreatedAt
                })
        }));
    }

    [HttpPost("{orderNumber}/payments")]
    public async Task<ActionResult<ApiResponse<object>>> AddPayment(
        string orderNumber,
        [FromBody] WebOrderPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _context.MobileOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
        {
            return NotFound(ApiResponse<object>.ErrorResult(
                "ORDER_NOT_FOUND",
                "سفارش یافت نشد."));
        }

        var total = order.Items.Sum(x => x.TotalPrice);
        var paid = await _context.MobileOrderPayments
            .Where(x => x.OrderId == order.Id)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var remaining = Math.Max(0m, total - paid);

        if (request.Amount <= 0)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "PAYMENT_AMOUNT_INVALID",
                "مبلغ پرداخت معتبر نیست."));
        }

        if (request.Amount > remaining)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(
                "PAYMENT_EXCEEDS_REMAINING",
                "مبلغ پرداخت از مانده سفارش بیشتر است.",
                new { remaining }));
        }

        var now = DateTime.UtcNow;
        var payment = new MobileOrderPayment
        {
            OrderId = order.Id,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate?.Trim() ?? now.ToString("yyyy-MM-dd HH:mm:ss"),
            TrackingNumber = request.TrackingNumber?.Trim(),
            BankName = request.BankName?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            CreatedBy = 0
        };

        await _context.MobileOrderPayments.AddAsync(payment, cancellationToken);
        order.PaymentAmount = paid + request.Amount;
        order.PaymentDate = payment.PaymentDate;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResult(new
        {
            order.OrderNumber,
            Payment = new
            {
                payment.Id,
                payment.Amount,
                payment.PaymentDate,
                payment.TrackingNumber,
                payment.BankName
            },
            TotalAmount = total,
            PaidAmount = order.PaymentAmount,
            RemainingAmount = Math.Max(0m, total - order.PaymentAmount)
        }, "پرداخت سفارش ثبت شد."));
    }

    private static string BuildOrderNumber(DateTime utcNow)
    {
        return $"W{utcNow:yyyyMMddHHmmssfff}";
    }

    private static async Task RollbackSafelyAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // Preserve the original exception.
        }
    }
}

public sealed class WebOrderPaymentRequest
{
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.001", "999999999999999")]
    public decimal Amount { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(20)]
    public string? PaymentDate { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string? TrackingNumber { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string? BankName { get; set; }

    public string? Notes { get; set; }
}
