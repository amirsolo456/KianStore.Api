using KianStore.Api.Data;
using KianStore.Api.Middleware;
using KianStore.Api.Repositories.Implementations;
using KianStore.Api.Repositories.Interfaces;
using KianStore.Api.Services.Implementations;
using KianStore.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentMutationService, DocumentMutationService>();
builder.Services.AddScoped<ISanadAuditService, SanadAuditService>();
builder.Services.AddScoped<DiscountCodeService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<OrderRegistrationSmsService>();
builder.Services.AddScoped<OrderRegistrationSmsServiceV2>();
builder.Services.AddScoped<MobileAuthService>();

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();
});

builder.Services.AddCors(options => options.AddPolicy("FlutterWeb", policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
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
        (ID, SanadTypeName, AnbarCaption, ToCaption, TarafType, BedBesType, ByAnbar, ByMali, Disable, IDFaktorAuto, SanadTypeMabna, SanadTypeMabna2, SanadTypeMabna3, IsAllowNoMabna, Emzae1, Emzae2, Emzae3, Emzae4)
        SELECT 113, N'فروش از انبار همکار', AnbarCaption, ToCaption, TarafType, BedBesType, ByAnbar, ByMali, Disable, IDFaktorAuto, SanadTypeMabna, SanadTypeMabna2, SanadTypeMabna3, IsAllowNoMabna, Emzae1, Emzae2, Emzae3, Emzae4
        FROM dbo.SanadType WHERE ID = 12;
    END;
    DECLARE @definition nvarchar(max);
    SELECT @definition = cc.definition FROM sys.check_constraints AS cc WHERE cc.name = N'CK_Sanad_SanadType' AND cc.parent_object_id = OBJECT_ID(N'dbo.Sanad');
    IF @definition IS NOT NULL AND @definition NOT LIKE N'%[[]SanadType[]] = 113%' AND @definition NOT LIKE N'%SanadType = 113%'
    BEGIN
        DECLARE @alterSql nvarchar(max);
        SET @alterSql = N'ALTER TABLE dbo.Sanad DROP CONSTRAINT [CK_Sanad_SanadType];'; EXEC sys.sp_executesql @alterSql;
        SET @alterSql = N'ALTER TABLE dbo.Sanad ADD CONSTRAINT [CK_Sanad_SanadType] CHECK (' + @definition + N' OR [SanadType] = 113);'; EXEC sys.sp_executesql @alterSql;
    END;
END;

IF OBJECT_ID(N'dbo.DiscountCode', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscountCode (Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscountCode PRIMARY KEY, Code varchar(50) NOT NULL, Title nvarchar(200) NULL, TakhfifId int NOT NULL, Type int NOT NULL CONSTRAINT DF_DiscountCode_Type DEFAULT ((1)), Scope int NOT NULL CONSTRAINT DF_DiscountCode_Scope DEFAULT ((1)), PersonId int NULL, IssuedForIdSal int NULL, IssuedForIdSanad varchar(10) NULL, Value decimal(18,3) NOT NULL, MaxDiscountAmount decimal(18,3) NULL, StartDate datetime2(0) NOT NULL, EndDate datetime2(0) NULL, UsageLimit int NULL, UsedCount int NOT NULL CONSTRAINT DF_DiscountCode_UsedCount DEFAULT ((0)), PerCustomerLimit int NULL, IsActive bit NOT NULL CONSTRAINT DF_DiscountCode_IsActive DEFAULT ((1)), Description nvarchar(1000) NULL, CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_DiscountCode_CreatedAt DEFAULT (SYSUTCDATETIME()), CONSTRAINT UQ_DiscountCode_Code UNIQUE (Code), CONSTRAINT FK_DiscountCode_Takhfif FOREIGN KEY (TakhfifId) REFERENCES dbo.Takhfif(ID));
    CREATE INDEX IX_DiscountCode_ScopePerson ON dbo.DiscountCode(Scope, PersonId, IsActive);
    CREATE INDEX IX_DiscountCode_IssuedFor ON dbo.DiscountCode(IssuedForIdSal, IssuedForIdSanad);
END;

IF OBJECT_ID(N'dbo.DiscountCodeUsage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscountCodeUsage (Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscountCodeUsage PRIMARY KEY, DiscountCodeId int NOT NULL, PersonId int NOT NULL, OrderAmount decimal(18,3) NOT NULL, DiscountAmount decimal(18,3) NOT NULL, IdSal int NULL, IdSanad varchar(10) NULL, UsedAt datetime2(0) NOT NULL CONSTRAINT DF_DiscountCodeUsage_UsedAt DEFAULT (SYSUTCDATETIME()), CONSTRAINT FK_DiscountCodeUsage_DiscountCode FOREIGN KEY (DiscountCodeId) REFERENCES dbo.DiscountCode(Id) ON DELETE CASCADE, CONSTRAINT FK_DiscountCodeUsage_Sanad FOREIGN KEY (IdSal, IdSanad) REFERENCES dbo.Sanad(IDSal, ID) ON DELETE SET NULL);
    CREATE INDEX IX_DiscountCodeUsage_CodePerson ON dbo.DiscountCodeUsage(DiscountCodeId, PersonId);
    CREATE INDEX IX_DiscountCodeUsage_Sanad ON dbo.DiscountCodeUsage(IdSal, IdSanad);
END;

IF OBJECT_ID(N'dbo.SmsTemplate', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsTemplate (Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsTemplate PRIMARY KEY, Name nvarchar(100) NOT NULL, TemplateText nvarchar(1000) NOT NULL, IsActive bit NOT NULL CONSTRAINT DF_SmsTemplate_IsActive DEFAULT ((1)), CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_SmsTemplate_CreatedAt DEFAULT (SYSUTCDATETIME()), UpdatedAt datetime2(0) NULL);
    CREATE INDEX IX_SmsTemplate_IsActive ON dbo.SmsTemplate(IsActive);
END;

IF OBJECT_ID(N'dbo.SmsLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsLog (Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsLog PRIMARY KEY, PersonId int NULL, IdSal int NULL, IdSanad nvarchar(10) NULL, Mobile varchar(70) NOT NULL, Message nvarchar(1000) NOT NULL, TemplateId int NULL, Status int NOT NULL CONSTRAINT DF_SmsLog_Status DEFAULT ((1)), Provider varchar(100) NULL, ProviderMessageId varchar(100) NULL, ProviderStatus int NULL, ProviderStatusText nvarchar(200) NULL, ErrorMessage nvarchar(500) NULL, LastStatusCheckedAt datetime2 NULL, CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_SmsLog_CreatedAt DEFAULT (SYSUTCDATETIME()), CONSTRAINT CK_SmsLog_Status CHECK (Status IN (1,2,3)), CONSTRAINT FK_SmsLog_Template FOREIGN KEY (TemplateId) REFERENCES dbo.SmsTemplate(Id) ON DELETE SET NULL);
    CREATE INDEX IX_SmsLog_PersonId_CreatedAt ON dbo.SmsLog(PersonId, CreatedAt DESC);
    CREATE INDEX IX_SmsLog_Document ON dbo.SmsLog(IdSal, IdSanad, CreatedAt DESC);
    CREATE INDEX IX_SmsLog_CreatedAt ON dbo.SmsLog(CreatedAt DESC);
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.SmsLog', N'IdSal') IS NULL ALTER TABLE dbo.SmsLog ADD IdSal int NULL;
    IF COL_LENGTH(N'dbo.SmsLog', N'IdSanad') IS NULL ALTER TABLE dbo.SmsLog ADD IdSanad nvarchar(10) NULL;
    IF COL_LENGTH(N'dbo.SmsLog', N'ProviderStatus') IS NULL ALTER TABLE dbo.SmsLog ADD ProviderStatus int NULL;
    IF COL_LENGTH(N'dbo.SmsLog', N'ProviderStatusText') IS NULL ALTER TABLE dbo.SmsLog ADD ProviderStatusText nvarchar(200) NULL;
    IF COL_LENGTH(N'dbo.SmsLog', N'LastStatusCheckedAt') IS NULL ALTER TABLE dbo.SmsLog ADD LastStatusCheckedAt datetime2 NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SmsLog_Document' AND object_id = OBJECT_ID(N'dbo.SmsLog')) CREATE INDEX IX_SmsLog_Document ON dbo.SmsLog(IdSal, IdSanad, CreatedAt DESC);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SmsTemplate WHERE Name = N'templatemobile')
BEGIN
    INSERT INTO dbo.SmsTemplate (Name, TemplateText, IsActive) VALUES (N'templatemobile', N'دامداری آریا دام خاتون\\nسفارش شما با شماره فاکتور : %token\\nبا موفقیت ثبت گردید💐🙏🏻\\nشماره تماس پشتیبان:\\n۰۹۱۹۲۴۱۰۲۰۷\\nاین کد رو میتونید در خرید بعدیتون استفاده کنید (کد هدیه) : %token3', 1);
END
ELSE
BEGIN
    UPDATE dbo.SmsTemplate SET TemplateText = N'دامداری آریا دام خاتون\\nسفارش شما با شماره فاکتور : %token\\nبا موفقیت ثبت گردید💐🙏🏻\\nشماره تماس پشتیبان:\\n۰۹۱۹۲۴۱۰۲۰۷\\nاین کد رو میتونید در خرید بعدیتون استفاده کنید (کد هدیه) : %token3', IsActive = 1, UpdatedAt = SYSUTCDATETIME() WHERE Name = N'templatemobile';
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SmsTemplate WHERE Name = N'sanadregistered')
BEGIN
    INSERT INTO dbo.SmsTemplate (Name, TemplateText, IsActive)
    VALUES (
        N'sanadregistered',
        N'آریا دام خاتون\\nخریدار گرامی، فاکتور %token به مبلغ %token2 تومان با موفقیت ثبت شد.\\nپشتیبانی: 09192410207',
        1
    );
END
ELSE
BEGIN
    UPDATE dbo.SmsTemplate
    SET TemplateText = N'آریا دام خاتون\\nخریدار گرامی، فاکتور %token به مبلغ %token2 تومان با موفقیت ثبت شد.\\nپشتیبانی: 09192410207',
        IsActive = 1,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Name = N'sanadregistered';
END;
";
    db.Database.ExecuteSqlRaw(sql);
}

app.UseMiddleware<GlobalExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("FlutterWeb");
app.UseAuthorization();
app.MapControllers();
app.Run();
