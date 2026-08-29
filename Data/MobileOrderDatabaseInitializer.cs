using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Data;

public static class MobileOrderDatabaseInitializer
{
    public static async Task InitializeAsync(
        KianStoreDbContext context,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.MobileOrders', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MobileOrders
                (
                    Id BIGINT IDENTITY(1,1) NOT NULL,
                    OrderNumber NVARCHAR(20) NOT NULL,
                    FirstName NVARCHAR(50) NOT NULL,
                    LastName NVARCHAR(50) NOT NULL,
                    Mobile NVARCHAR(20) NOT NULL,
                    Address NVARCHAR(MAX) NULL,
                    PaymentDate NVARCHAR(20) NULL,
                    PaymentAmount DECIMAL(18,3) NOT NULL CONSTRAINT DF_MobileOrders_PaymentAmount DEFAULT (0),
                    Status INT NOT NULL CONSTRAINT DF_MobileOrders_Status DEFAULT (1),
                    TarafId INT NULL,
                    TarafType INT NULL,
                    SanadId NVARCHAR(20) NULL,
                    SanadSal INT NULL,
                    Notes NVARCHAR(MAX) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MobileOrders_CreatedAt DEFAULT (GETDATE()),
                    CreatedBy INT NOT NULL CONSTRAINT DF_MobileOrders_CreatedBy DEFAULT (0),
                    CONSTRAINT PK_MobileOrders PRIMARY KEY (Id),
                    CONSTRAINT UQ_MobileOrders_OrderNumber UNIQUE (OrderNumber)
                );

                CREATE INDEX IX_MobileOrders_Mobile
                    ON dbo.MobileOrders (Mobile);

                CREATE INDEX IX_MobileOrders_Status_CreatedAt
                    ON dbo.MobileOrders (Status, CreatedAt DESC);
            END;

            IF OBJECT_ID(N'dbo.MobileOrderItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MobileOrderItems
                (
                    Id BIGINT IDENTITY(1,1) NOT NULL,
                    OrderId BIGINT NOT NULL,
                    KalaId VARCHAR(20) NOT NULL,
                    Quantity DECIMAL(18,3) NOT NULL,
                    UnitPrice DECIMAL(18,3) NOT NULL,
                    TotalPrice DECIMAL(18,3) NOT NULL,
                    CONSTRAINT PK_MobileOrderItems PRIMARY KEY (Id),
                    CONSTRAINT FK_MobileOrderItems_MobileOrders
                        FOREIGN KEY (OrderId)
                        REFERENCES dbo.MobileOrders (Id)
                        ON DELETE CASCADE
                );

                CREATE INDEX IX_MobileOrderItems_OrderId
                    ON dbo.MobileOrderItems (OrderId);

                CREATE INDEX IX_MobileOrderItems_KalaId
                    ON dbo.MobileOrderItems (KalaId);
            END;

            IF OBJECT_ID(N'dbo.MobileOrderPayments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MobileOrderPayments
                (
                    Id BIGINT IDENTITY(1,1) NOT NULL,
                    OrderId BIGINT NOT NULL,
                    PaymentDate NVARCHAR(20) NOT NULL,
                    Amount DECIMAL(18,3) NOT NULL,
                    TrackingNumber NVARCHAR(50) NULL,
                    BankName NVARCHAR(50) NULL,
                    Notes NVARCHAR(MAX) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MobileOrderPayments_CreatedAt DEFAULT (GETDATE()),
                    CreatedBy INT NOT NULL CONSTRAINT DF_MobileOrderPayments_CreatedBy DEFAULT (0),
                    CONSTRAINT PK_MobileOrderPayments PRIMARY KEY (Id),
                    CONSTRAINT FK_MobileOrderPayments_MobileOrders
                        FOREIGN KEY (OrderId)
                        REFERENCES dbo.MobileOrders (Id)
                        ON DELETE CASCADE
                );

                CREATE INDEX IX_MobileOrderPayments_OrderId
                    ON dbo.MobileOrderPayments (OrderId);
            END;
            """;

        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
