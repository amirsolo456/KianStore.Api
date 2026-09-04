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
        throw new FileNotFoundException($"server.config.txt not found: {path}");

    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            continue;

        var separator = line.IndexOf('=');
        if (separator <= 0)
            continue;

        values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
    }

    return values;
}

var serverConfig = LoadServerConfig();

var builder = WebApplication.CreateBuilder(args);

var connectionString = serverConfig.GetValueOrDefault("ConnectionString", string.Empty);
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionString is missing in server.config.txt.");

var bindAddress = serverConfig.GetValueOrDefault("ApiBindAddress", "127.0.0.1");
var port = serverConfig.GetValueOrDefault("ApiPort", "5000");
builder.WebHost.UseUrls($"http://{bindAddress}:{port}");

builder.Configuration["ConnectionStrings:KianStore"] = connectionString;
builder.Configuration["Sms:Provider"] = serverConfig.GetValueOrDefault("SmsProvider", "HttpSmsProvider");
builder.Configuration["Sms:SendUrl"] = serverConfig.GetValueOrDefault("SmsSendUrl", string.Empty);
builder.Configuration["Sms:ApiKey"] = serverConfig.GetValueOrDefault("SmsApiKey", string.Empty);
builder.Configuration["Sms:Sender"] = serverConfig.GetValueOrDefault("SmsSender", string.Empty);

builder.Services.AddControllers();

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

var configuredOrigins = serverConfig.GetValueOrDefault("CorsAllowedOrigins", string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddPolicy("FlutterWeb", policy =>
{
    if (configuredOrigins.Length > 0)
        policy.WithOrigins(configuredOrigins).AllowAnyHeader().AllowAnyMethod();
    else
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
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
