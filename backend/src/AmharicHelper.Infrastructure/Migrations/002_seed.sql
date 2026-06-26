-- Seed a demo user for local development.
-- Password is "Password1!" hashed with the app's PBKDF2 hasher (see PasswordHasher).
-- Idempotent: ON CONFLICT on the unique Email index makes re-runs a no-op.

INSERT INTO Users (Id, Email, PasswordHash, DisplayName, PreferredLanguage, CreatedAt)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'demo@amharichelper.local',
    -- PBKDF2 hash of "Password1!" (format: {iterations}.{saltB64}.{hashB64})
    '100000.6Ru3qzj0nQ2bVnQY1bq2A==.k0Yc5kZqN0c2t8xY3m8oQ6m1Q1m9d3jJ5rXl3xKQ0bM=',
    'Demo User',
    0,
    now()
)
ON CONFLICT (Email) DO NOTHING;
