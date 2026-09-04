using KianStore.Api.Data;
using KianStore.Api.Middleware;
using KianStore.Api.Repositories.Implementations;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Implementations;
using KianStore.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

static Dictionary<string, string> LoadServerConfig()
{
    var path = Path.Combine(AppContext.BaseDirectory, "server.config.txt");
    if (!File.Exists(path))
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            continue;

        var separator = line.IndexOf('=');
        if (separator <= 0)
            continue;

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();
        values[key] = value;
    }

    return values;
}

var serverConfig = LoadServerConfig();

var builder = WebApplication.CreateBuilder(args);

// server.config.txt is the single source of truth for production server settings.
// Environment variables and normal ASP.NET configuration can still override these values.
if (serverConfig.TryGetValue("ConnectionString", out var configuredConnectionString) &&
    !string.IsNullOrWhiteSpace(configuredConnectionString) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__KianStore")))
{
    builder.Configuration["ConnectionStrings:KianStore"] = configuredConnectionString;
}

if (serverConfig.TryGetValue("CorsAllowedOrigins", out var allowedOriginsText) &&
    !string.IsNullOrWhiteSpace(allowedOriginsText))
{
    var origins = allowedOriginsText
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    builder.Configuration["Cors:AllowedOrigins"] = string.Join(',', origins);
}

if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var devHost = serverConfig.GetValueOrDefault("ApiBindAddress", "0.0.0.0");
    var devPort = serverConfig.GetValueOrDefault("ApiPort", "5069");
    builder.WebHost.UseUrls($"http://{devHost}:{devPort}");
}

if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var bindAddress = serverConfig.GetValueOrDefault("ApiBindAddress", "127.0.0.1");
    var port = serverConfig.GetValueOrDefault("ApiPort", "5000");
    builder.WebHost.UseUrls($"http://{bindAddress}:{port}");
}

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("KianStore")
    ?? throw new InvalidOperationException("ConnectionString is missing. Check server.config.txt.");

builder.Services.AddDbContext<KianStoreDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        sql.CommandTimeout(60);
    }));

builder.Services.AddHttpClient("SmsProvider", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IDocumentService, LegacyDocumentService>();
builder.Services.AddScoped<DiscountCodeService>();
builder.Services.AddScoped<SmsService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options => options.AddPolicy("FlutterWeb", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
    else
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }
}));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("FlutterWeb");
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    success = true,
    service = "KianStore.Api",
    environment = app.Environment.EnvironmentName,
    message = "API در حال اجراست.",
    health = "/api/health",
    publicBaseUrl = serverConfig.GetValueOrDefault("PublicBaseUrl", string.Empty)
}));

app.MapControllers();

app.Run();
