-- Account lockout after repeated failed login attempts. Additional brute-force hardening
-- on top of the per-IP rate limiter (AuthController's [EnableRateLimiting("auth")]).
-- Idempotent: safe to run on every startup.

ALTER TABLE Users ADD COLUMN IF NOT EXISTS FailedLoginAttempts INT NOT NULL DEFAULT 0;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS LockoutEndsAt TIMESTAMPTZ NULL;
