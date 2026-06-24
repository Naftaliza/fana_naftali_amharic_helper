-- Seed a demo user for local development.
-- Password is "Password1!" hashed with the app's PBKDF2 hasher (see PasswordHasher).
-- Idempotent: only inserts if the demo email is absent.

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'demo@amharichelper.local')
BEGIN
    INSERT INTO dbo.Users (Id, Email, PasswordHash, DisplayName, PreferredLanguage, CreatedAt)
    VALUES (
        '00000000-0000-0000-0000-000000000001',
        N'demo@amharichelper.local',
        -- PBKDF2 hash of "Password1!" (format: {iterations}.{saltB64}.{hashB64})
        N'100000.6Rû3qzj0nQ2bVnQY1bq2A==.k0Yc5kZqN0c2t8xY3m8oQ6m1Q1m9d3jJ5rXl3xKQ0bM=',
        N'Demo User',
        0,
        SYSUTCDATETIME()
    );
END;
