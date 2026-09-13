using System.Security.Cryptography;
using KianStore.Api.Common;
using KianStore.Api.Data;
using KianStore.Api.DTOs.Products;
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
            .Where(x => !x.IsDisabled && !string.IsNullOrWhiteSpace(x.Id) && x.Id != "00" && x.Id != "01");

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim()
                .Replace("ي", "ی").Replace("ى", "ی").Replace("ك", "ک")
                .Replace("ۀ", "ه").Replace("ة", "ه").Replace("‌", " ");

            query = query.Where(x =>
                x.Id.Contains(search) ||
                x.KalaName.Replace("ي", "ی").Replace("ى", "ی").Replace("ك", "ک")
                    .Replace("ۀ", "ه").Replace("ة", "ه").Replace("‌", " ").Contains(search));
        }

        var products = string.IsNullOrWhiteSpace(search)
            ? await query.OrderBy(x => x.KalaName).ToListAsync(cancellationToken)
            : await query.OrderBy(x => x.KalaName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Ok(ApiResponse<IEnumerable<Kala>>.SuccessResult(products));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Kala>>> GetProduct(string id, CancellationToken cancellationToken)
    {
        var product = await _context.Kalas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product == null)
            return NotFound(ApiResponse<Kala>.ErrorResult("PRODUCT_NOT_FOUND", "کالا یافت نشد."));

        return Ok(ApiResponse<Kala>.SuccessResult(product));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Kala>>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (name.Length == 0)
            return BadRequest(ApiResponse<Kala>.ErrorResult("INVALID_PRODUCT", "نام کالا الزامی است."));

        // Product code is an internal database key and is never supplied by the client.
        // Keep the existing Kala.ID schema unchanged and generate a numeric 18-digit code here.
        string code;
        do
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = RandomNumberGenerator.GetInt32(0, 1_000_000);
            code = $"{timestamp}{random:D6}";
        }
        while (await _context.Kalas.AnyAsync(x => x.Id == code, cancellationToken));

        var product = new Kala
        {
            Id = code,
            KalaName = name,
            IdSanjesh = request.UnitId <= 0 ? 1 : request.UnitId,
            KalaType = request.TypeId <= 0 ? 1 : request.TypeId,
            MabFrosh = request.SalePrice < 0 ? 0 : request.SalePrice,
            MabKharid = request.PurchasePrice < 0 ? 0 : request.PurchasePrice,
            IsDisabled = false,
            IdAnbarFrosh = 1,
            MinCount = 0,
            IdSanjesh2 = request.UnitId <= 0 ? 1 : request.UnitId,
            Quantity = 0,
            Barcode = request.Barcode?.Trim() ?? string.Empty,
        };

        _context.Kalas.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<Kala>.SuccessResult(product));
    }
}
