using KianStore.Api.Data;
using KianStore.Api.Middleware;
using KianStore.Api.Repositories.Implementations;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Implementations;
using KianStore.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Local development keeps the historical 5069 port. Under IIS/ASP.NET Core Module,
// IIS owns the listener and Kestrel receives its binding from the hosting module.
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5069");
}

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("KianStore")
    ?? throw new InvalidOperationException("ConnectionStrings:KianStore is missing.");

builder.Services.AddDbContext<KianStoreDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHttpClient("SmsProvider", client => client.Timeout = TimeSpan.FromSeconds(20));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<DiscountCodeService>();
builder.Services.AddScoped<SmsService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy("FlutterWeb", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
    else
    {
        // Native Flutter clients do not send an Origin header. Keep the current
        // behaviour for installations that do not host Flutter Web, while allowing
        // production web deployments to lock this down via Cors:AllowedOrigins.
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }
}));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // TLS is terminated by IIS in the Windows Server deployment.
    app.UseHttpsRedirection();
}

app.UseCors("FlutterWeb");
app.UseAuthorization();
app.MapControllers();
app.Run();
