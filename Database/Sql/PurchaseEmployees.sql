USE [KianStore_2]
GO

IF OBJECT_ID(N'dbo.PurchaseEmployees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseEmployees
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseEmployees PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Mobile NVARCHAR(70) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_PurchaseEmployees_IsActive DEFAULT ((1))
    );
    CREATE INDEX IX_PurchaseEmployees_IsActive_Name ON dbo.PurchaseEmployees(IsActive, Name);
END
GO

IF COL_LENGTH(N'dbo.Sanad', N'PurchaseEmployeeId') IS NULL
BEGIN
    ALTER TABLE dbo.Sanad ADD PurchaseEmployeeId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Sanad_PurchaseEmployee'
      AND parent_object_id = OBJECT_ID(N'dbo.Sanad')
)
BEGIN
    ALTER TABLE dbo.Sanad
    ADD CONSTRAINT FK_Sanad_PurchaseEmployee
        FOREIGN KEY (PurchaseEmployeeId) REFERENCES dbo.PurchaseEmployees(Id);
END
GO
