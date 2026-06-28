-- Business self-registration: applicants submit via the public /partners page and land here
-- as PENDING providers (IsActive = FALSE) until an admin approves them. ContactEmail is how
-- we reach the applicant. Pending vs live is expressed by the existing IsActive column.

ALTER TABLE Providers ADD COLUMN IF NOT EXISTS ContactEmail TEXT NULL;

-- Speeds up the admin "pending review" listing.
CREATE INDEX IF NOT EXISTS IX_Providers_Active_Created ON Providers (IsActive, CreatedAt);
