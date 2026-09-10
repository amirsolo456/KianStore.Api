IF OBJECT_ID(N'dbo.KalaWeb', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KalaWeb
    (
        IDKala varchar(20) NOT NULL,
        Slug nvarchar(200) NULL,
        ShortDescription nvarchar(500) NULL,
        Description nvarchar(max) NULL,
        MainImageUrl nvarchar(1000) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_KalaWeb_IsActive DEFAULT (1),
        DisplayOrder int NOT NULL CONSTRAINT DF_KalaWeb_DisplayOrder DEFAULT (0),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_KalaWeb_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_KalaWeb_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_KalaWeb PRIMARY KEY (IDKala),
        CONSTRAINT FK_KalaWeb_Kala FOREIGN KEY (IDKala) REFERENCES dbo.Kala(ID)
    );

    CREATE UNIQUE INDEX UX_KalaWeb_Slug
        ON dbo.KalaWeb(Slug)
        WHERE Slug IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.KalaWebImage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KalaWebImage
    (
        ID bigint IDENTITY(1,1) NOT NULL,
        IDKala varchar(20) NOT NULL,
        ImageUrl nvarchar(1000) NOT NULL,
        AltText nvarchar(300) NULL,
        DisplayOrder int NOT NULL CONSTRAINT DF_KalaWebImage_DisplayOrder DEFAULT (0),
        IsMain bit NOT NULL CONSTRAINT DF_KalaWebImage_IsMain DEFAULT (0),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_KalaWebImage_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_KalaWebImage PRIMARY KEY (ID),
        CONSTRAINT FK_KalaWebImage_KalaWeb FOREIGN KEY (IDKala) REFERENCES dbo.KalaWeb(IDKala) ON DELETE CASCADE
    );

    CREATE INDEX IX_KalaWebImage_IDKala_DisplayOrder
        ON dbo.KalaWebImage(IDKala, DisplayOrder);
END
GO
