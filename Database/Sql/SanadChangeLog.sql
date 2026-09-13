USE [KianStore_2]
GO

-- 113 = فروش از انبار همکار.
-- The row is cloned from normal sale type 12 so all required accounting flags
-- are populated consistently; only ID/name are specialized here.
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
        113, 'فروش از انبار همکار', AnbarCaption, ToCaption, TarafType, BedBesType,
        ByAnbar, ByMali, Disable, IDFaktorAuto, SanadTypeMabna,
        SanadTypeMabna2, SanadTypeMabna3, IsAllowNoMabna,
        Emzae1, Emzae2, Emzae3, Emzae4
    FROM dbo.SanadType
    WHERE ID = 12;

    IF @@ROWCOUNT = 0
        THROW 50001, 'SanadType=12 was not found; cannot initialize SanadType=113 safely.', 1;
END
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

-- Audit records are written for CREATE, UPDATE and DELETE.
-- The API reads X-User-Id, resolves the user from dbo.Users,
-- and stores ID + UserName + UserFLName in SanadChangeLog.
