using KianStore.Api.Data;
using KianStore.Api.Models;
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
    /// دریافت لیست کالاهای قابل سفارش
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Kala>>> GetProducts(
        CancellationToken cancellationToken)
    {
        var products = await _context.Kalas
            .AsNoTracking()
            .Where(x =>
                !x.IsDisabled &&
                !string.IsNullOrWhiteSpace(x.Id) &&
                x.Id != "00" &&
                x.Id != "01")
            .OrderBy(x => x.KalaName)
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    /// <summary>
    /// دریافت یک کالا بر اساس شناسه
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Kala>> GetProduct(
        string id,
        CancellationToken cancellationToken)
    {
        var product = await _context.Kalas
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (product == null)
            return NotFound();

        return Ok(product);
    }
}