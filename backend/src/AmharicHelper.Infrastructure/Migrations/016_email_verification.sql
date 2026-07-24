-- Email verification for new registrations. Registration no longer issues JWTs immediately;
-- the account is created unverified and a single-use, hashed, time-limited token is emailed
-- (see RegisterHandler/VerifyEmailHandler). Idempotent: safe to run on every startup.

ALTER TABLE Users ADD COLUMN IF NOT EXISTS EmailVerified BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS EmailVerificationTokenHash TEXT NULL;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS EmailVerificationExpiresAt TIMESTAMPTZ NULL;

-- Users created before this migration went through the old (no-verification) flow — treat
-- them as already verified rather than locking them out. Only legacy rows match this WHERE
-- clause: any account created by the new RegisterHandler always has a non-null token hash
-- until verified, and verified accounts already have EmailVerified = TRUE.
UPDATE Users SET EmailVerified = TRUE
WHERE EmailVerified = FALSE AND EmailVerificationTokenHash IS NULL AND EmailVerificationExpiresAt IS NULL;
