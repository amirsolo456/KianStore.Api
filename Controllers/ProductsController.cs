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
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Kala>>> GetProducts(
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
            search = search.Trim();
            query = query.Where(x =>
                x.Id.Contains(search) ||
                x.KalaName.Contains(search) ||
                x.Barcode.Contains(search));
        }

        var products = await query
            .OrderBy(x => x.KalaName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Kala>> GetProduct(
        string id,
        CancellationToken cancellationToken)
    {
        var product = await _context.Kalas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (product == null)
            return NotFound();

        return Ok(product);
    }
}
