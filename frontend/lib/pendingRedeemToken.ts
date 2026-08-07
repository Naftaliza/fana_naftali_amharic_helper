// Stashes a gift-redeem token when someone follows a /redeem/{token} link but isn't signed in
// yet — the single most likely case for this exact feature, since the beneficiary (an older
// relative with no Fana account) is often opening the link for the very first time. Same typed
// localStorage pattern as lib/pendingTrialAnalysis.ts; RedeemPendingPrompt picks this up once
// the user is authenticated, wherever they land after registering/logging in.

const KEY = "pendingRedeemToken";

export function setPendingRedeemToken(token: string): void {
  window.localStorage.setItem(KEY, token);
}

export function getPendingRedeemToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(KEY);
}

export function clearPendingRedeemToken(): void {
  window.localStorage.removeItem(KEY);
}
