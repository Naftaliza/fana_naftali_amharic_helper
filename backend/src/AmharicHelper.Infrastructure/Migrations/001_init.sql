-- Amharic Helper - initial schema (PostgreSQL). Idempotent: safe to run on every startup.
-- Unquoted identifiers fold to lowercase; Dapper maps columns to entity properties
-- case-insensitively, so the PascalCase names here still bind correctly.

CREATE TABLE IF NOT EXISTS Users (
    Id                UUID        NOT NULL PRIMARY KEY,
    Email             TEXT        NOT NULL,
    PasswordHash      TEXT        NOT NULL,
    DisplayName       TEXT        NOT NULL,
    PreferredLanguage INT         NOT NULL DEFAULT 0,
    CreatedAt         TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Users_Email ON Users(Email);

CREATE TABLE IF NOT EXISTS RefreshTokens (
    Id        UUID        NOT NULL PRIMARY KEY,
    UserId    UUID        NOT NULL REFERENCES Users(Id),
    Token     TEXT        NOT NULL,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    RevokedAt TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS IX_RefreshTokens_UserId ON RefreshTokens(UserId);
CREATE INDEX IF NOT EXISTS IX_RefreshTokens_Token ON RefreshTokens(Token);

CREATE TABLE IF NOT EXISTS Documents (
    Id          UUID        NOT NULL PRIMARY KEY,
    UserId      UUID        NOT NULL REFERENCES Users(Id),
    FileName    TEXT        NOT NULL,
    FilePath    TEXT        NOT NULL,
    ContentType TEXT        NOT NULL,
    OcrText     TEXT        NULL,
    UploadedAt  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS IX_Documents_UserId ON Documents(UserId);

CREATE TABLE IF NOT EXISTS DocumentAnalyses (
    Id                  UUID        NOT NULL PRIMARY KEY,
    DocumentId          UUID        NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
    Summary             TEXT        NOT NULL DEFAULT '{}',
    DocumentType        TEXT        NOT NULL DEFAULT '{}',
    UrgencyLevel        INT         NOT NULL DEFAULT 0,
    KeyPointsJson       TEXT        NOT NULL DEFAULT '[]',
    RequiredActionsJson TEXT        NOT NULL DEFAULT '[]',
    DeadlinesJson       TEXT        NOT NULL DEFAULT '[]',
    ExplanationJson     TEXT        NOT NULL DEFAULT '{}',
    CreatedAt           TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS IX_DocumentAnalyses_DocumentId ON DocumentAnalyses(DocumentId);

CREATE TABLE IF NOT EXISTS ChatMessages (
    Id         UUID        NOT NULL PRIMARY KEY,
    DocumentId UUID        NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
    Role       INT         NOT NULL,
    Content    TEXT        NOT NULL,
    CreatedAt  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS IX_ChatMessages_DocumentId ON ChatMessages(DocumentId);
