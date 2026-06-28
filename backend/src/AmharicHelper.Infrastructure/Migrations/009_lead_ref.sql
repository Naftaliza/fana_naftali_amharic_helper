-- Short reference code per lead, surfaced to the user and prefilled into the WhatsApp message
-- ("via Fana #ABC123") so the provider can quote it and you can match/confirm the lead.

ALTER TABLE Leads ADD COLUMN IF NOT EXISTS Ref TEXT NULL;
