-- Per-lead price for each provider, used to compute monthly invoice amounts on the Leads tab.
-- Defaults to 0 (set per provider via the admin Manage tab).

ALTER TABLE Providers ADD COLUMN IF NOT EXISTS PricePerLead NUMERIC(10,2) NOT NULL DEFAULT 0;
