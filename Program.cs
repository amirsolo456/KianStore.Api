using KianStore.Api.Data;
using KianStore.Api.Middleware;
using KianStore.Api.Repositories.Implementations;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Implementations;
using KianStore.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Keep the historical 5069 port only for local Development when no explicit URL exists.
// On Windows Server/IIS the host owns the listener/binding. For a direct production
// smoke test, ASPNETCORE_URLS can be set explicitly from PowerShell.
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5069");
}

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("KianStore")
    ?? throw new InvalidOperationException("ConnectionStrings:KianStore is missing.");

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
builder.Services.AddScoped<IDocumentService, DocumentService>();
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
        // Native Flutter clients do not send an Origin header. Web deployments
        // should set Cors:AllowedOrigins explicitly in Production.
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }
}));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("FlutterWeb");
app.UseAuthorization();
app.MapControllers();

app.Run();
