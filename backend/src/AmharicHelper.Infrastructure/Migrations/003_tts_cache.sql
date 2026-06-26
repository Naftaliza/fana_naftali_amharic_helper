-- 003: cache synthesized text-to-speech audio per (document, language) so replaying
-- an explanation, or re-opening a document, does not re-bill the speech provider.
-- Invalidated in application code when a document is re-analyzed; ON DELETE CASCADE
-- removes cached audio automatically when the document itself is deleted.
IF OBJECT_ID(N'dbo.TtsAudioCache', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TtsAudioCache (
        DocumentId  UNIQUEIDENTIFIER NOT NULL,
        Language    INT              NOT NULL,
        ContentType NVARCHAR(100)    NOT NULL,
        Audio       VARBINARY(MAX)   NOT NULL,
        CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TtsAudioCache PRIMARY KEY (DocumentId, Language),
        CONSTRAINT FK_TtsAudioCache_Documents FOREIGN KEY (DocumentId)
            REFERENCES dbo.Documents(Id) ON DELETE CASCADE
    );
END;
