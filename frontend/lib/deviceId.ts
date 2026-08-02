// A per-browser id sent as X-Device-Id on anonymous trial requests, so the backend's UsageLedger
// (see WalletService) can meter an anonymous visitor across multiple uploads instead of falling
// back to a coarse per-IP subject shared by everyone behind the same NAT/carrier. Still
// spoofable by clearing storage — the point is a real server-side ceiling exists at all, not a
// tamper-proof one; see lib/trial.ts, which this replaces (that counter reset on its own with no
// server involved).

const KEY = "deviceId";

export function getDeviceId(): string {
  if (typeof window === "undefined") return "";
  let id = window.localStorage.getItem(KEY);
  if (!id) {
    id = crypto.randomUUID();
    window.localStorage.setItem(KEY, id);
  }
  return id;
}
