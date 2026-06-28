-- Distinguish NEW applications (never reviewed) from providers an admin has acted on, so a
-- deactivated provider doesn't reappear in the "pending review" queue.
--   pending review : IsActive = FALSE AND ReviewedAt IS NULL
--   live           : IsActive = TRUE
--   deactivated    : IsActive = FALSE AND ReviewedAt IS NOT NULL
-- Existing active providers (samples / already approved) are treated as already reviewed.

ALTER TABLE Providers ADD COLUMN IF NOT EXISTS ReviewedAt TIMESTAMPTZ NULL;

UPDATE Providers SET ReviewedAt = now() WHERE IsActive = TRUE AND ReviewedAt IS NULL;
