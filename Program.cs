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
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentMutationService, DocumentMutationService>();
builder.Services.AddScoped<ISanadAuditService, SanadAuditService>();
builder.Services.AddScoped<DiscountCodeService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<OrderRegistrationSmsService>();
builder.Services.AddScoped<MobileAuthService>();

var jwtKey = JwtKeyProvider.GetOrCreate(builder.Configuration);
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

// Mobile authentication is intentionally one-time login only. All API endpoints
// are allowed without a Bearer token so the Flutter client does not need to send
// or refresh any token after the initial username/password check.
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();
});

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
END;

IF OBJECT_ID(N'dbo.SmsLog', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.SmsLog', N'IdSal') IS NULL ALTER TABLE dbo.SmsLog ADD IdSal int NULL;
    IF COL_LENGTH(N'dbo.SmsLog', N'IdSanad') IS NULL ALTER TABLE dbo.SmsLog ADD IdSanad nvarchar(10) NULL;
    IF COL_LENGTH(N'dbo.SmsLog', N'ProviderStatus') IS NULL ALTER TABLE dbo.SmsLog ADD ProviderStatus int NULL;
    IF COL_LENGTH(N'dbo.SmsLog', N'ProviderStatusText') IS NULL ALTER TABLE dbo.SmsLog ADD ProviderStatusText nvarchar(200) NULL;
    IF COL_LENGTH(N'dbo.SmsLog', N'LastStatusCheckedAt') IS NULL ALTER TABLE dbo.SmsLog ADD LastStatusCheckedAt datetime2 NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SmsLog_Document' AND object_id = OBJECT_ID(N'dbo.SmsLog'))
        CREATE INDEX IX_SmsLog_Document ON dbo.SmsLog(IdSal, IdSanad, CreatedAt DESC);
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
