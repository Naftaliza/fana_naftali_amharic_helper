-- Lead lifecycle status (bill on real conversions, not raw taps) and post-contact
-- "was this helpful?" feedback (a quality signal for the provider).
ALTER TABLE Leads ADD COLUMN IF NOT EXISTS Status INT NOT NULL DEFAULT 0;
ALTER TABLE Leads ADD COLUMN IF NOT EXISTS Helpful BOOLEAN NULL;
