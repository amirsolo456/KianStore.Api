using KianStore.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly KianStoreDbContext _context;

    public HealthController(KianStoreDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    success = false,
                    message = "اتصال API برقرار است اما اتصال به دیتابیس برقرار نیست.",
                    service = "KianStore.Api",
                    database = "unavailable"
                });
            }

            // Also verify that the customers table/query path used by the app is available.
            await _context.Tarafs
                .AsNoTracking()
                .Select(x => x.Id)
                .Take(1)
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                message = "API و دیتابیس آماده هستند.",
                service = "KianStore.Api",
                database = "ok",
                timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                message = "API در دسترس است اما بررسی دیتابیس یا جدول مشتریان با خطا مواجه شد.",
                service = "KianStore.Api",
                database = "error",
                error = ex.Message
            });
        }
    }
}
