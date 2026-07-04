-- Performance indexes for hot query paths (org stats dashboard, auth, referral lookups).
-- Idempotent: safe to run on every startup.

-- Org stats dashboard joins DocumentAnalyses -> Documents -> Users filtered by
-- Users.OrganizationId; without this index that join is a full scan of Users.
CREATE INDEX IF NOT EXISTS IX_Users_OrganizationId ON Users(OrganizationId) WHERE OrganizationId IS NOT NULL;

-- Weekly trend grouping (date_trunc('week', CreatedAt)) and category/urgency breakdowns
-- scan DocumentAnalyses; an index on CreatedAt lets Postgres use an index scan for the
-- common "recent activity" access pattern instead of a sequential scan as the table grows.
CREATE INDEX IF NOT EXISTS IX_DocumentAnalyses_CreatedAt ON DocumentAnalyses(CreatedAt);

-- Organizations lookup by slug is on the hot path for every tenant-branded page load
-- (GET /api/organizations/{slug}), currently served by the unique index on LOWER(Slug)
-- from migration 012. Nothing further needed there; this file documents the audit.
