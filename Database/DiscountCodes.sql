/*
    Coupon layer for the existing KianStore discount engine.
    Takhfif remains the source of discount rules; these tables only add
    coupon-code identity and usage tracking.
*/

IF OBJECT_ID(N'dbo.DiscountCode', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscountCode
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscountCode PRIMARY KEY,
        Code varchar(50) NOT NULL,
        Title nvarchar(200) NULL,
        TakhfifId int NOT NULL,
        Type int NOT NULL CONSTRAINT DF_DiscountCode_Type DEFAULT ((1)),
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
        CONSTRAINT CK_DiscountCode_Value CHECK (Value > 0),
        CONSTRAINT CK_DiscountCode_Percentage CHECK (Type <> 1 OR Value <= 100),
        CONSTRAINT CK_DiscountCode_Date CHECK (EndDate IS NULL OR EndDate >= StartDate),
        CONSTRAINT CK_DiscountCode_Usage CHECK (UsageLimit IS NULL OR UsageLimit >= 0),
        CONSTRAINT CK_DiscountCode_PerCustomer CHECK (PerCustomerLimit IS NULL OR PerCustomerLimit >= 0),
        CONSTRAINT FK_DiscountCode_Takhfif FOREIGN KEY (TakhfifId) REFERENCES dbo.Takhfif(ID)
    );
END
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
        CONSTRAINT FK_DiscountCodeUsage_DiscountCode FOREIGN KEY (DiscountCodeId)
            REFERENCES dbo.DiscountCode(Id) ON DELETE CASCADE,
        CONSTRAINT FK_DiscountCodeUsage_Sanad FOREIGN KEY (IdSal, IdSanad)
            REFERENCES dbo.Sanad(IDSal, ID) ON DELETE SET NULL
    );

    CREATE INDEX IX_DiscountCodeUsage_CodePerson
        ON dbo.DiscountCodeUsage(DiscountCodeId, PersonId);

    CREATE INDEX IX_DiscountCodeUsage_Sanad
        ON dbo.DiscountCodeUsage(IdSal, IdSanad);
END
GO
