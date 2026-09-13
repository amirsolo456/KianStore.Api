USE [KianStore_2]
GO

IF OBJECT_ID(N'dbo.SanadChangeLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SanadChangeLog
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SanadChangeLog PRIMARY KEY,
        IdSal INT NOT NULL,
        IdSanad VARCHAR(20) NOT NULL,
        SanadType INT NOT NULL,
        [Action] VARCHAR(20) NOT NULL,
        UserId INT NULL,
        UserName NVARCHAR(100) NULL,
        UserFullName NVARCHAR(150) NULL,
        ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_SanadChangeLog_ChangedAt DEFAULT SYSUTCDATETIME(),
        [Description] NVARCHAR(500) NULL
    );

    CREATE INDEX IX_SanadChangeLog_Document
        ON dbo.SanadChangeLog(IdSal, IdSanad, ChangedAt DESC);

    CREATE INDEX IX_SanadChangeLog_User
        ON dbo.SanadChangeLog(UserId, ChangedAt DESC);
END
GO

-- Examples:
-- 113 = فروش از انبار همکار
-- CREATE is written when the document is created, UPDATE on edit, DELETE on soft-delete.
-- The backend resolves the user from X-User-Id and stores UserName/UserFullName from dbo.Users.
