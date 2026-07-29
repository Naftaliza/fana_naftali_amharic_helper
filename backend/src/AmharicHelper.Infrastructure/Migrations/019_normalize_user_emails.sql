-- Emails were never normalized before this migration: RegisterHandler stored whatever casing
-- was typed, and every lookup (login, password reset, email verification) did an exact,
-- case-sensitive match. A user who typed their email with different capitalization between
-- registration and a later login (autofill, mobile keyboard, password manager) would get a
-- generic "invalid email or password" even with the right password, because the lookup found
-- no matching row. Application code now normalizes to lower/trim on every write and read
-- (see EmailNormalizer) — this backfills existing rows to match. Idempotent: re-running only
-- touches rows that still differ.
--
-- Skip any row whose normalized form would collide with another existing row (e.g. someone
-- registered twice with different casing, believing their first account was lost) — Email has
-- a unique index, so normalizing both would throw and crash the whole migration run/API
-- startup. Those rare cases are left as-is for manual review instead:
--   SELECT Email FROM Users GROUP BY LOWER(TRIM(Email)) HAVING COUNT(*) > 1;
UPDATE Users u
SET Email = LOWER(TRIM(u.Email))
WHERE u.Email <> LOWER(TRIM(u.Email))
  AND NOT EXISTS (
    SELECT 1 FROM Users u2
    WHERE u2.Id <> u.Id AND LOWER(TRIM(u2.Email)) = LOWER(TRIM(u.Email))
  );
