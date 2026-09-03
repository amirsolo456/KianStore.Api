using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.Models.KianStore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly KianStoreDbContext _context;

    public ProductsController(KianStoreDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// دریافت و جستجوی کالاهای قابل استفاده.
    /// جستجو فقط بر اساس ID و KalaName انجام می‌شود.
    /// در حالت بدون عبارت جستجو، تمام کالاهای فعال برگردانده می‌شوند.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<Kala>>>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Kalas
            .AsNoTracking()
            .Where(x =>
                !x.IsDisabled &&
                !string.IsNullOrWhiteSpace(x.Id) &&
                x.Id != "00" &&
                x.Id != "01");

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search
                .Trim()
                .Replace("ي", "ی")
                .Replace("ى", "ی")
                .Replace("ك", "ک")
                .Replace("ۀ", "ه")
                .Replace("ة", "ه")
                .Replace("‌", " ");

            query = query.Where(x =>
                x.Id.Contains(search) ||
                x.KalaName
                    .Replace("ي", "ی")
                    .Replace("ى", "ی")
                    .Replace("ك", "ک")
                    .Replace("ۀ", "ه")
                    .Replace("ة", "ه")
                    .Replace("‌", " ")
                    .Contains(search));
        }

        var products = string.IsNullOrWhiteSpace(search)
            ? await query
                .OrderBy(x => x.KalaName)
                .ToListAsync(cancellationToken)
            : await query
                .OrderBy(x => x.KalaName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IEnumerable<Kala>>.SuccessResult(products));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Kala>>> GetProduct(
        string id,
        CancellationToken cancellationToken)
    {
        var product = await _context.Kalas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (product == null)
        {
            return NotFound(ApiResponse<Kala>.ErrorResult(
                "PRODUCT_NOT_FOUND",
                "کالا یافت نشد."));
        }

        return Ok(ApiResponse<Kala>.SuccessResult(product));
    }
}
