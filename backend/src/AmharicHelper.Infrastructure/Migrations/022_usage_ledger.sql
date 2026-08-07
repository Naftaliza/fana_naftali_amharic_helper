-- Server-side usage metering. The only usage cap in the app before this migration was
-- frontend/lib/trial.ts — a localStorage counter cleared by clearing browser data — and
-- DocumentsController had no rate limiting at all, so the authenticated analyze endpoint was
-- completely uncapped against paid Anthropic/Azure keys. This table is the real meter: an
-- append-only ledger where a subject's balance is always SUM(Delta), never a mutable counter
-- (see WalletService). Idempotent: safe to run on every startup.

CREATE TABLE IF NOT EXISTS UsageLedger (
    Id          UUID        NOT NULL PRIMARY KEY,
    UserId      UUID NULL REFERENCES Users(Id) ON DELETE CASCADE,
    ContactHash TEXT NULL,
    Kind        INT         NOT NULL,             -- 0=Grant 1=Consume 2=Expire 3=Refund 4=Sponsorship
    Delta       INT         NOT NULL,
    Operation   TEXT        NOT NULL DEFAULT '',   -- 'analyze' | 'tts' | 'chat' | 'free_tier_monthly'
    CostUsd     NUMERIC(10,5) NULL,
    DocumentId  UUID NULL,
    Note        TEXT        NOT NULL DEFAULT '',
    CreatedAt   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT CK_UsageLedger_Subject CHECK (UserId IS NOT NULL OR ContactHash IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS IX_UsageLedger_User    ON UsageLedger(UserId, CreatedAt DESC);
CREATE INDEX IF NOT EXISTS IX_UsageLedger_Contact ON UsageLedger(ContactHash, CreatedAt DESC);
