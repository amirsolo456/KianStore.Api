USE [master];
GO

IF SUSER_ID(N'IIS APPPOOL\KianStore.Api') IS NULL
    CREATE LOGIN [IIS APPPOOL\KianStore.Api] FROM WINDOWS;
GO

USE [KianStore_2];
GO

IF USER_ID(N'IIS APPPOOL\KianStore.Api') IS NULL
    CREATE USER [IIS APPPOOL\KianStore.Api] FOR LOGIN [IIS APPPOOL\KianStore.Api];
GO

ALTER ROLE [db_datareader] ADD MEMBER [IIS APPPOOL\KianStore.Api];
ALTER ROLE [db_datawriter] ADD MEMBER [IIS APPPOOL\KianStore.Api];
GO

-- EF Core and the application execute stored procedures that already exist in the legacy DB.
-- Grant execute at database scope so the API can use those procedures without dbo rights.
GRANT EXECUTE TO [IIS APPPOOL\KianStore.Api];
GO
