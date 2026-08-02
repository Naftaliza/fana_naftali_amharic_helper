-- Sponsorship: someone else pays. A sponsor gifts credits from their own balance to a
-- beneficiary, redeemable via a single-use link. The sponsor's balance is debited at creation
-- time (see WalletService.TryDebitForSponsorshipAsync), not at redemption, so a link can never
-- promise credits the sponsor doesn't have. Idempotent: safe to run on every startup.

CREATE TABLE IF NOT EXISTS Sponsorships (
    Id                     UUID        NOT NULL PRIMARY KEY,
    SponsorUserId          UUID        NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    BeneficiaryContactHash TEXT        NOT NULL,
    Credits                INT         NOT NULL,
    RedeemTokenHash        TEXT        NOT NULL,
    RedeemedByUserId       UUID NULL REFERENCES Users(Id) ON DELETE SET NULL,
    RedeemedAt             TIMESTAMPTZ NULL,
    ExpiresAt              TIMESTAMPTZ NOT NULL,
    CreatedAt              TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- The redemption lookup path: find the one row matching a token's hash directly (see the
-- Sponsorship entity doc comment on why this hash is deterministic SHA-256, not PBKDF2).
CREATE UNIQUE INDEX IF NOT EXISTS UX_Sponsorships_RedeemTokenHash ON Sponsorships(RedeemTokenHash);
CREATE INDEX IF NOT EXISTS IX_Sponsorships_SponsorUserId ON Sponsorships(SponsorUserId, CreatedAt DESC);
