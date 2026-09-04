/* Discount code tables */
IF OBJECT_ID(N'dbo.DiscountCode', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscountCode
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscountCode PRIMARY KEY,
        Code varchar(50) NOT NULL,
        Title nvarchar(200) NULL,
        TakhfifId int NOT NULL,
        Type int NOT NULL CONSTRAINT DF_DiscountCode_Type DEFAULT ((1)),
        Scope int NOT NULL CONSTRAINT DF_DiscountCode_Scope DEFAULT ((1)),
        PersonId int NULL,
        IssuedForIdSal int NULL,
        IssuedForIdSanad varchar(10) NULL,
        Value decimal(18,3) NOT NULL,
        MaxDiscountAmount decimal(18,3) NULL,
        StartDate datetime2(0) NOT NULL,
        EndDate datetime2(0) NULL,
        UsageLimit int NULL,
        UsedCount int NOT NULL CONSTRAINT DF_DiscountCode_UsedCount DEFAULT ((0)),
        PerCustomerLimit int NULL,
        IsActive bit NOT NULL CONSTRAINT DF_DiscountCode_IsActive DEFAULT ((1)),
        Description nvarchar(1000) NULL,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_DiscountCode_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_DiscountCode_Code UNIQUE (Code),
        CONSTRAINT CK_DiscountCode_Type CHECK (Type IN (1,2)),
        CONSTRAINT CK_DiscountCode_Scope CHECK (Scope IN (1,2)),
        CONSTRAINT CK_DiscountCode_PrivatePerson CHECK (Scope = 1 OR PersonId IS NOT NULL),
        CONSTRAINT CK_DiscountCode_Value CHECK (Value > 0),
        CONSTRAINT CK_DiscountCode_Percentage CHECK (Type <> 1 OR Value <= 100),
        CONSTRAINT CK_DiscountCode_Date CHECK (EndDate IS NULL OR EndDate >= StartDate),
        CONSTRAINT CK_DiscountCode_Usage CHECK (UsageLimit IS NULL OR UsageLimit >= 0),
        CONSTRAINT CK_DiscountCode_PerCustomer CHECK (PerCustomerLimit IS NULL OR PerCustomerLimit >= 0),
        CONSTRAINT FK_DiscountCode_Takhfif FOREIGN KEY (TakhfifId) REFERENCES dbo.Takhfif(ID)
    );
    CREATE INDEX IX_DiscountCode_ScopePerson ON dbo.DiscountCode(Scope, PersonId, IsActive);
    CREATE INDEX IX_DiscountCode_IssuedFor ON dbo.DiscountCode(IssuedForIdSal, IssuedForIdSanad);
END
GO

/* Existing installations: migrate added columns without dropping existing data. */
IF COL_LENGTH(N'dbo.DiscountCode', N'Scope') IS NULL
    ALTER TABLE dbo.DiscountCode ADD Scope int NOT NULL CONSTRAINT DF_DiscountCode_Scope_Migration DEFAULT ((1));
GO
IF COL_LENGTH(N'dbo.DiscountCode', N'PersonId') IS NULL
    ALTER TABLE dbo.DiscountCode ADD PersonId int NULL;
GO
IF COL_LENGTH(N'dbo.DiscountCode', N'IssuedForIdSal') IS NULL
    ALTER TABLE dbo.DiscountCode ADD IssuedForIdSal int NULL;
GO
IF COL_LENGTH(N'dbo.DiscountCode', N'IssuedForIdSanad') IS NULL
    ALTER TABLE dbo.DiscountCode ADD IssuedForIdSanad varchar(10) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DiscountCode_Scope' AND parent_object_id = OBJECT_ID(N'dbo.DiscountCode'))
    ALTER TABLE dbo.DiscountCode ADD CONSTRAINT CK_DiscountCode_Scope CHECK (Scope IN (1,2));
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DiscountCode_PrivatePerson' AND parent_object_id = OBJECT_ID(N'dbo.DiscountCode'))
    ALTER TABLE dbo.DiscountCode ADD CONSTRAINT CK_DiscountCode_PrivatePerson CHECK (Scope = 1 OR PersonId IS NOT NULL);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DiscountCode_ScopePerson' AND object_id = OBJECT_ID(N'dbo.DiscountCode'))
    CREATE INDEX IX_DiscountCode_ScopePerson ON dbo.DiscountCode(Scope, PersonId, IsActive);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DiscountCode_IssuedFor' AND object_id = OBJECT_ID(N'dbo.DiscountCode'))
    CREATE INDEX IX_DiscountCode_IssuedFor ON dbo.DiscountCode(IssuedForIdSal, IssuedForIdSanad);
GO

IF OBJECT_ID(N'dbo.DiscountCodeUsage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscountCodeUsage
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscountCodeUsage PRIMARY KEY,
        DiscountCodeId int NOT NULL,
        PersonId int NOT NULL,
        OrderAmount decimal(18,3) NOT NULL,
        DiscountAmount decimal(18,3) NOT NULL,
        IdSal int NULL,
        IdSanad varchar(10) NULL,
        UsedAt datetime2(0) NOT NULL CONSTRAINT DF_DiscountCodeUsage_UsedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_DiscountCodeUsage_DiscountCode FOREIGN KEY (DiscountCodeId) REFERENCES dbo.DiscountCode(Id) ON DELETE CASCADE,
        CONSTRAINT FK_DiscountCodeUsage_Sanad FOREIGN KEY (IdSal, IdSanad) REFERENCES dbo.Sanad(IDSal, ID) ON DELETE SET NULL
    );
    CREATE INDEX IX_DiscountCodeUsage_CodePerson ON dbo.DiscountCodeUsage(DiscountCodeId, PersonId);
    CREATE INDEX IX_DiscountCodeUsage_Sanad ON dbo.DiscountCodeUsage(IdSal, IdSanad);
END
GO

IF OBJECT_ID(N'dbo.SmsTemplate', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsTemplate
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsTemplate PRIMARY KEY,
        Name nvarchar(100) NOT NULL,
        TemplateText nvarchar(1000) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_SmsTemplate_IsActive DEFAULT ((1)),
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_SmsTemplate_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt datetime2(0) NULL
    );
    CREATE INDEX IX_SmsTemplate_IsActive ON dbo.SmsTemplate(IsActive);
END
GO

IF OBJECT_ID(N'dbo.SmsLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsLog
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsLog PRIMARY KEY,
        PersonId int NULL,
        Mobile varchar(70) NOT NULL,
        Message nvarchar(1000) NOT NULL,
        TemplateId int NULL,
        Status int NOT NULL CONSTRAINT DF_SmsLog_Status DEFAULT ((1)),
        Provider varchar(100) NULL,
        ProviderMessageId varchar(100) NULL,
        ErrorMessage nvarchar(500) NULL,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_SmsLog_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_SmsLog_Status CHECK (Status IN (1,2,3)),
        CONSTRAINT FK_SmsLog_Template FOREIGN KEY (TemplateId) REFERENCES dbo.SmsTemplate(Id) ON DELETE SET NULL
    );
    CREATE INDEX IX_SmsLog_PersonId_CreatedAt ON dbo.SmsLog(PersonId, CreatedAt DESC);
    CREATE INDEX IX_SmsLog_CreatedAt ON dbo.SmsLog(CreatedAt DESC);
END
GO
