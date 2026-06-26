-- Amharic Helper — initial schema. Idempotent: safe to run on every startup.

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Email             NVARCHAR(256)    NOT NULL,
        PasswordHash      NVARCHAR(512)    NOT NULL,
        DisplayName       NVARCHAR(200)    NOT NULL,
        PreferredLanguage INT              NOT NULL DEFAULT 0,
        CreatedAt         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email);
END;

IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens (
        Id        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId    UNIQUEIDENTIFIER NOT NULL,
        Token     NVARCHAR(512)    NOT NULL,
        ExpiresAt DATETIME2        NOT NULL,
        CreatedAt DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RevokedAt DATETIME2        NULL,
        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_RefreshTokens_UserId ON dbo.RefreshTokens(UserId);
    CREATE INDEX IX_RefreshTokens_Token ON dbo.RefreshTokens(Token);
END;

IF OBJECT_ID(N'dbo.Documents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Documents (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId      UNIQUEIDENTIFIER NOT NULL,
        FileName    NVARCHAR(400)    NOT NULL,
        FilePath    NVARCHAR(1000)   NOT NULL,
        ContentType NVARCHAR(200)    NOT NULL,
        OcrText     NVARCHAR(MAX)    NULL,
        UploadedAt  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Documents_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_Documents_UserId ON dbo.Documents(UserId);
END;

IF OBJECT_ID(N'dbo.DocumentAnalyses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentAnalyses (
        Id                     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DocumentId             UNIQUEIDENTIFIER NOT NULL,
        Summary                NVARCHAR(MAX)    NOT NULL DEFAULT N'{}',
        DocumentType           NVARCHAR(MAX)    NOT NULL DEFAULT N'{}',
        UrgencyLevel           INT              NOT NULL DEFAULT 0,
        KeyPointsJson          NVARCHAR(MAX)    NOT NULL DEFAULT N'[]',
        RequiredActionsJson    NVARCHAR(MAX)    NOT NULL DEFAULT N'[]',
        DeadlinesJson          NVARCHAR(MAX)    NOT NULL DEFAULT N'[]',
        ExplanationJson        NVARCHAR(MAX)    NOT NULL DEFAULT N'{}',
        CreatedAt              DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_DocumentAnalyses_Documents FOREIGN KEY (DocumentId) REFERENCES dbo.Documents(Id)
    );
    CREATE INDEX IX_DocumentAnalyses_DocumentId ON dbo.DocumentAnalyses(DocumentId);
END;

IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatMessages (
        Id         UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        DocumentId UNIQUEIDENTIFIER NOT NULL,
        Role       INT              NOT NULL,
        Content    NVARCHAR(MAX)    NOT NULL,
        CreatedAt  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ChatMessages_Documents FOREIGN KEY (DocumentId) REFERENCES dbo.Documents(Id)
    );
    CREATE INDEX IX_ChatMessages_DocumentId ON dbo.ChatMessages(DocumentId);
END;
