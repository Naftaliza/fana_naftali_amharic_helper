-- Minimal funnel instrumentation (see plan). Before this, the app had zero product analytics —
-- no way to see signups, verification completion, activation (first upload), or return visits.
-- Intentionally just a name + optional user + timestamp, not a general-purpose event schema.
CREATE TABLE IF NOT EXISTS AnalyticsEvents (
    Id UUID PRIMARY KEY,
    Name TEXT NOT NULL,
    UserId UUID NULL REFERENCES Users(Id) ON DELETE SET NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS IX_AnalyticsEvents_Name ON AnalyticsEvents(Name);
CREATE INDEX IF NOT EXISTS IX_AnalyticsEvents_CreatedAt ON AnalyticsEvents(CreatedAt);
