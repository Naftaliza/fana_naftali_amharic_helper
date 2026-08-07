// Simple env-var feature flags. NEXT_PUBLIC_* vars are inlined into the client bundle at BUILD
// time (see frontend/Dockerfile), not read at runtime — flipping one means rebuilding and
// redeploying, not editing a running container's environment. That tradeoff is fine here: this
// is a toggle meant to be flipped rarely (a launch date), not a live A/B switch.

// Wallet/credits/gifting: hidden by default. There is no real payment rail yet — credits are
// still granted manually, out-of-band (see WalletController.AdminGrant) — so showing a live
// "wallet balance" and "gift credits" flow in front of an investor (or any user) raises
// questions the product isn't ready to answer. The backend usage ledger itself is untouched by
// this flag and keeps metering analyze/speech/chat silently in the background either way —
// OutOfCreditsPrompt still works, it just no longer links anywhere that isn't reachable.
// Flip NEXT_PUBLIC_FEATURE_WALLET=true (and rebuild) once there's a real self-serve credit story.
export const FEATURE_WALLET = process.env.NEXT_PUBLIC_FEATURE_WALLET === "true";
