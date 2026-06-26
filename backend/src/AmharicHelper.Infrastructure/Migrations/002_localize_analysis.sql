-- 002: localize the analysis content to all three languages.
-- Every textual field becomes a JSON object { "he", "am", "en" }. The legacy
-- single-language columns (TranslatedAmharic / TranslatedSimpleHebrew, and the
-- plain-text Summary/DocumentType) cannot be migrated in place, so where the legacy
-- schema is detected we rebuild the table. Old analyses are discarded; documents can
-- simply be re-analyzed. Idempotent: once migrated, the legacy column is gone and this
-- block is skipped. Fresh databases get the new shape from 001 and never enter here.
IF OBJECT_ID(N'dbo.DocumentAnalyses', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.DocumentAnalyses', 'TranslatedAmharic') IS NOT NULL
BEGIN
    DROP TABLE dbo.DocumentAnalyses;

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
