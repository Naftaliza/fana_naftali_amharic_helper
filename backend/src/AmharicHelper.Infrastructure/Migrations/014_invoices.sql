-- Provider invoicing: persisted, immutable snapshots of billable (Converted) leads per
-- provider + calendar month, generated on admin demand and emailed as a PDF. Snapshotting
-- (rather than the live GetSummaryAsync query) means a lead's status changing later never
-- silently changes a historical invoice total.

CREATE TABLE IF NOT EXISTS Invoices (
    Id UUID PRIMARY KEY,
    ProviderId UUID NOT NULL REFERENCES Providers(Id),
    InvoiceNumber TEXT NOT NULL,
    PeriodYear INT NOT NULL,
    PeriodMonth INT NOT NULL,
    PricePerLead NUMERIC(10,2) NOT NULL,
    Currency TEXT NOT NULL DEFAULT 'ILS',
    TotalAmount NUMERIC(12,2) NOT NULL,
    LeadCount INT NOT NULL,
    LineItemsJson TEXT NOT NULL DEFAULT '[]',
    Status INT NOT NULL DEFAULT 0,
    SentToEmail TEXT NOT NULL DEFAULT '',
    GeneratedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    SentAt TIMESTAMPTZ NULL,
    SendError TEXT NULL
);

-- Idempotency guard: one invoice per provider per calendar month.
CREATE UNIQUE INDEX IF NOT EXISTS UX_Invoices_Provider_Period
    ON Invoices(ProviderId, PeriodYear, PeriodMonth);

CREATE INDEX IF NOT EXISTS IX_Invoices_ProviderId ON Invoices(ProviderId);
