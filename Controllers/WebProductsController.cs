using System.Data;
using KianStore.Api.Common;
using KianStore.Api.DTOs.WebProducts;
using KianStore.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace KianStore.Api.Controllers;

/// <summary>
/// Read-only product endpoints for the WordPress website.
/// Uses the existing Kala/KalaAdv tables. No web-specific schema is created.
/// Existing mobile/legacy APIs are intentionally untouched.
/// </summary>
[ApiController]
[Route("api/web/products")]
public sealed class WebProductsController : ControllerBase
{
    private readonly KianStoreDbContext _context;
    private readonly IConfiguration _configuration;

    public WebProductsController(KianStoreDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WebProductResponse>>>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var normalizedSearch = NormalizePersian(search);
        var offset = (page - 1) * pageSize;

        const string sql = """
            SELECT
                k.ID AS Id,
                k.KalaName AS Name,
                k.MabFrosh AS Price,
                ISNULL(k.Quantity, 0) AS Stock,
                k.Barcode AS Barcode,
                CAST(CASE WHEN k.IsDisabled = 0 THEN 1 ELSE 0 END AS bit) AS IsActive,
                ka.Des AS Description,
                ka.Des1 AS ShortDescription
            FROM dbo.Kala AS k
            LEFT JOIN dbo.KalaAdv AS ka ON ka.IDKala = k.ID
            WHERE k.IsDisabled = 0
              AND NULLIF(LTRIM(RTRIM(k.ID)), '') IS NOT NULL
              AND k.ID NOT IN ('00', '01')
              AND (
                    @search = ''
                    OR k.ID LIKE '%' + @search + '%'
                    OR ISNULL(k.Barcode, '') LIKE '%' + @search + '%'
                    OR REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(k.KalaName, N'ي', N'ی'), N'ى', N'ی'), N'ك', N'ک'), N'ۀ', N'ه'), N'ة', N'ه'), NCHAR(8204), N' ') LIKE '%' + @search + '%'
                  )
            ORDER BY k.KalaName, k.ID
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 30
        };

        command.Parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar, 200) { Value = normalizedSearch });
        command.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int) { Value = offset });
        command.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int) { Value = pageSize });

        var rows = new List<WebProductResponse>(pageSize);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(reader.GetOrdinal("Id"));
            var product = Map(reader, id);
            product.ImageUrls = BuildImageUrls(product.Id);
            product.MainImageUrl = product.ImageUrls[0];
            rows.Add(product);
        }

        return Ok(ApiResponse<IReadOnlyList<WebProductResponse>>.SuccessResult(rows));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<WebProductResponse>>> GetProduct(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(ApiResponse<WebProductResponse>.ErrorResult("INVALID_PRODUCT_ID", "شناسه کالا معتبر نیست."));

        const string sql = """
            SELECT TOP (1)
                k.ID AS Id,
                k.KalaName AS Name,
                k.MabFrosh AS Price,
                ISNULL(k.Quantity, 0) AS Stock,
                k.Barcode AS Barcode,
                CAST(CASE WHEN k.IsDisabled = 0 THEN 1 ELSE 0 END AS bit) AS IsActive,
                ka.Des AS Description,
                ka.Des1 AS ShortDescription
            FROM dbo.Kala AS k
            LEFT JOIN dbo.KalaAdv AS ka ON ka.IDKala = k.ID
            WHERE k.ID = @id;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 30
        };
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.VarChar, 20) { Value = id.Trim() });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return NotFound(ApiResponse<WebProductResponse>.ErrorResult("PRODUCT_NOT_FOUND", "کالا یافت نشد."));

        var product = Map(reader, id.Trim());
        product.ImageUrls = BuildImageUrls(product.Id);
        product.MainImageUrl = product.ImageUrls[0];

        return Ok(ApiResponse<WebProductResponse>.SuccessResult(product));
    }

    [HttpGet("{id}/images/{slot:int}")]
    public async Task<IActionResult> GetImage(
        string id,
        int slot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || slot is < 1 or > 4)
            return NotFound();

        var column = slot switch
        {
            1 => "Image1",
            2 => "Image2",
            3 => "Image3",
            4 => "Image4",
            _ => string.Empty
        };

        if (column.Length == 0)
            return NotFound();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand($"SELECT {column} FROM dbo.KalaAdv WHERE IDKala = @id;", connection)
        {
            CommandTimeout = 30
        };
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.VarChar, 20) { Value = id.Trim() });

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value == DBNull.Value)
            return NotFound();

        var bytes = (byte[])value;
        if (bytes.Length == 0)
            return NotFound();

        return File(bytes, DetectImageContentType(bytes));
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("KianStore");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("KianStore connection string is not configured.");
        return new SqlConnection(connectionString);
    }

    private static WebProductResponse Map(System.Data.Common.DbDataReader reader, string id)
    {
        return new WebProductResponse
        {
            Id = id,
            Name = reader["Name"]?.ToString()?.Trim() ?? string.Empty,
            Price = reader["Price"] is DBNull ? 0m : Convert.ToDecimal(reader["Price"]),
            Stock = reader["Stock"] is DBNull ? 0m : Convert.ToDecimal(reader["Stock"]),
            Barcode = reader["Barcode"] is DBNull ? null : reader["Barcode"]?.ToString()?.Trim(),
            IsActive = reader["IsActive"] is not DBNull && Convert.ToBoolean(reader["IsActive"]),
            Description = reader["Description"] is DBNull ? null : reader["Description"]?.ToString(),
            ShortDescription = reader["ShortDescription"] is DBNull ? null : reader["ShortDescription"]?.ToString()
        };
    }

    private string[] BuildImageUrls(string id)
        => Enumerable.Range(1, 4)
            .Select(slot => $"{GetBaseUrl()}/api/web/products/{Uri.EscapeDataString(id)}/images/{slot}")
            .ToArray();

    private static string NormalizePersian(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .Replace("ي", "ی")
            .Replace("ى", "ی")
            .Replace("ك", "ک")
            .Replace("ۀ", "ه")
            .Replace("ة", "ه")
            .Replace("‌", " ");
    }

    private string GetBaseUrl()
        => $"{Request.Scheme}://{Request.Host.Value}";

    private static string DetectImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return "image/gif";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";
        return "application/octet-stream";
    }
}
