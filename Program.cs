using System.Security.Cryptography;
using System.Text;
using KianStore.Api.Data;
using KianStore.Api.Middleware;
using KianStore.Api.Repositories.Implementations;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Implementations;
using KianStore.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5069");

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

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
builder.Services.AddScoped<IDocumentMutationService, DocumentMutationService>();
builder.Services.AddScoped<ISanadAuditService, SanadAuditService>();
builder.Services.AddScoped<DiscountCodeService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<MobileAuthService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("Jwt__Key");
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    var securityDirectory = Path.Combine(AppContext.BaseDirectory, ".security");
    Directory.CreateDirectory(securityDirectory);
    var keyPath = Path.Combine(securityDirectory, "jwt.key");

    if (File.Exists(keyPath))
    {
        jwtKey = File.ReadAllText(keyPath).Trim();
    }

    if (Encoding.UTF8.GetByteCount(jwtKey ?? string.Empty) < 32)
    {
        jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        File.WriteAllText(keyPath, jwtKey);
    }

    builder.Configuration["Jwt:Key"] = jwtKey;
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "KianStore.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "KianStore.Mobile";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options => options.AddPolicy("FlutterWeb", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KianStoreDbContext>();
    const string sql = @"
IF OBJECT_ID(N'dbo.Sanad', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.SanadType WHERE ID = 113)
    BEGIN
        INSERT INTO dbo.SanadType
        (
            ID, SanadTypeName, AnbarCaption, ToCaption, TarafType, BedBesType,
            ByAnbar, ByMali, Disable, IDFaktorAuto, SanadTypeMabna,
            SanadTypeMabna2, SanadTypeMabna3, IsAllowNoMabna,
            Emzae1, Emzae2, Emzae3, Emzae4
        )
        SELECT
            113, N'فروش از انبار همکار', AnbarCaption, ToCaption, TarafType, BedBesType,
            ByAnbar, ByMali, Disable, IDFaktorAuto, SanadTypeMabna,
            SanadTypeMabna2, SanadTypeMabna3, IsAllowNoMabna,
            Emzae1, Emzae2, Emzae3, Emzae4
        FROM dbo.SanadType
        WHERE ID = 12;
    END;

    DECLARE @definition nvarchar(max);
    SELECT @definition = cc.definition
    FROM sys.check_constraints AS cc
    WHERE cc.name = N'CK_Sanad_SanadType'
      AND cc.parent_object_id = OBJECT_ID(N'dbo.Sanad');

    IF @definition IS NOT NULL
       AND @definition NOT LIKE N'%[[]SanadType[]] = 113%'
       AND @definition NOT LIKE N'%SanadType = 113%'
    BEGIN
        DECLARE @sql nvarchar(max);
        SET @sql = N'ALTER TABLE dbo.Sanad DROP CONSTRAINT [CK_Sanad_SanadType];';
        EXEC sys.sp_executesql @sql;

        SET @sql = N'ALTER TABLE dbo.Sanad ADD CONSTRAINT [CK_Sanad_SanadType] CHECK ('
                 + @definition + N' OR [SanadType] = 113);';
        EXEC sys.sp_executesql @sql;
    END;
END;";
    db.Database.ExecuteSqlRaw(sql);
}

app.UseMiddleware<GlobalExceptionMiddleware>();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); } else { app.UseHttpsRedirection(); }
app.UseCors("FlutterWeb");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
