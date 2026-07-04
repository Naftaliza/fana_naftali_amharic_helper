-- B2B/B2G tenants ("Organizations"): institutions (municipalities, health funds, NGOs) that
-- license Fana for their own residents/members, with their own branding. A user tagged with
-- OrganizationId is one of that tenant's members; consumer sign-ups and anonymous trials are
-- unaffected (OrganizationId stays NULL). The document pipeline itself is not tenant-specific —
-- usage is attributed to a tenant by joining Documents -> Users -> Organizations.

CREATE TABLE IF NOT EXISTS Organizations (
    Id              UUID        NOT NULL,
    Name            TEXT        NOT NULL,
    Slug            TEXT        NOT NULL,
    LogoUrl         TEXT        NULL,
    PrimaryColorHex TEXT        NOT NULL DEFAULT '#2563EB',
    AccentColorHex  TEXT        NULL,
    WelcomeText     TEXT        NOT NULL DEFAULT '{}',  -- LocalizedText JSON {"He","Am","En"}
    IsActive        BOOLEAN     NOT NULL DEFAULT TRUE,
    CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT PK_Organizations PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Organizations_Slug ON Organizations (LOWER(Slug));

ALTER TABLE Users ADD COLUMN IF NOT EXISTS OrganizationId UUID NULL REFERENCES Organizations(Id);
CREATE INDEX IF NOT EXISTS IX_Users_OrganizationId ON Users (OrganizationId);
