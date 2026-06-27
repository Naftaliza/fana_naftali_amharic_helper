-- Password reset support. Adds single-use, time-limited reset token storage to Users.
-- Only the HASH of the token is stored, so a database leak can't be used to reset passwords.
-- Idempotent: safe to run on every startup.

ALTER TABLE Users ADD COLUMN IF NOT EXISTS PasswordResetTokenHash TEXT        NULL;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS PasswordResetExpiresAt TIMESTAMPTZ NULL;
