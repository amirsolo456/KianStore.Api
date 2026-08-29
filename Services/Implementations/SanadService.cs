using Microsoft.EntityFrameworkCore;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Models.Orders;
using KianStore.Api.Services.Interfaces;

namespace KianStore.Api.Services.Implementations;

public class SanadService : ISanadService
{
    private readonly KianStoreDbContext _context;

    public SanadService(KianStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<string>> ConvertToSanadAsync(long mobileOrderId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.MobileOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == mobileOrderId);

            if (order == null)
                return ApiResponse<string>.ErrorResult("ORDER_NOT_FOUND", "سفارش یافت نشد.");

            if (order.Status != MobileOrderStatus.Confirmed)
                return ApiResponse<string>.ErrorResult("INVALID_STATUS", "فقط سفارشات تایید شده قابل تبدیل به سند هستند.");

            if (!string.IsNullOrEmpty(order.SanadId))
                return ApiResponse<string>.ErrorResult("ALREADY_CONVERTED", "این سفارش قبلاً به سند تبدیل شده است.");

            if (!order.TarafId.HasValue)
                return ApiResponse<string>.ErrorResult("MISSING_CUSTOMER", "مشتری برای این سفارش در کیان‌استور ثبت نشده است.");

            // 1. Prepare Sanad Header
            var sanadId = (await _context.Sanads.MaxAsync(s => (long?)s.Id) ?? 0) + 1;
            var faktorId = (await _context.Sanads.Where(s => s.SanadType == 12).MaxAsync(s => (int?)s.IdFaktor) ?? 0) + 1;

            // Note: In real KianStore, you might need to handle the current Persian Year (SanadSal)
            // and the specific Date format (yyyy/MM/dd).
            var sanad = new Sanad
            {
                Id = sanadId,
                IdTaraf = order.TarafId.Value,
                IdTarafType = order.TarafType ?? 1,
                SanadType = 12, // Sales Invoice
                SabtDate = DateTime.Now.ToString("yyyy/MM/dd"), // Simplified, should be Shamsi
                IdAnbar = 1, // Default Anbar
                IdMasool = 1, // Default User
                IdFaktor = faktorId,
                SanadSal = DateTime.Now.Year, // Simplified
                Description = $"Mobile Order: {order.OrderNumber}. {order.Notes}",
                TotalAmount = order.Items.Sum(i => i.TotalPrice)
            };

            _context.Sanads.Add(sanad);

            // 2. Prepare Sanad Details
            foreach (var item in order.Items)
            {
                var detail = new SanadDetail
                {
                    Id = sanadId,
                    IdKala = item.KalaId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                };
                _context.SanadDetails.Add(detail);
            }

            // 3. Update Order Status
            order.Status = MobileOrderStatus.ConvertedToSanad;
            order.SanadId = sanadId.ToString();
            order.SanadSal = sanad.SanadSal;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResponse<string>.SuccessResult(sanadId.ToString(), "سفارش با موفقیت به سند فروش تبدیل شد.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return ApiResponse<string>.ErrorResult("CONVERSION_ErrorResultED", $"خطا در تبدیل سند: {ex.Message}");
        }
    }
}
