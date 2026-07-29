// Thin fetch wrapper around the ASP.NET Core API with JWT handling.

import type {
  AccountExport,
  AnalysisResult,
  AuthUser,
  ChatMessage,
  CreateOrganizationPayload,
  DocumentDetail,
  DocumentSummary,
  EventDetail,
  Funnel,
  Invoice,
  LeadsOverview,
  ManagedProvider,
  OrganizationBranding,
  OrganizationStats,
  OrganizationSummary,
  PendingProvider,
  Provider,
  ProviderApplication,
  UpdateOrganizationPayload,
  UpdateProvider,
  UploadDocumentResult,
} from "@/lib/types";

const BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

const TOKEN_KEY = "accessToken";
const REFRESH_KEY = "refreshToken";

// "Remember me" support: tokens live in localStorage (persists across browser restarts) or
// sessionStorage (cleared when the tab/browser closes), never both — set() always clears the
// other storage so a later login with a different `remember` value can't leave a stale session
// sitting in the storage the reader doesn't expect.
export const tokenStore = {
  get access() {
    if (typeof window === "undefined") return null;
    return window.localStorage.getItem(TOKEN_KEY) ?? window.sessionStorage.getItem(TOKEN_KEY);
  },
  get refresh() {
    if (typeof window === "undefined") return null;
    return window.localStorage.getItem(REFRESH_KEY) ?? window.sessionStorage.getItem(REFRESH_KEY);
  },
  set(access: string, refresh: string, remember: boolean) {
    const active = remember ? window.localStorage : window.sessionStorage;
    const inactive = remember ? window.sessionStorage : window.localStorage;
    active.setItem(TOKEN_KEY, access);
    active.setItem(REFRESH_KEY, refresh);
    inactive.removeItem(TOKEN_KEY);
    inactive.removeItem(REFRESH_KEY);
  },
  clear() {
    window.localStorage.removeItem(TOKEN_KEY);
    window.localStorage.removeItem(REFRESH_KEY);
    window.sessionStorage.removeItem(TOKEN_KEY);
    window.sessionStorage.removeItem(REFRESH_KEY);
  },
};

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: AuthUser;
}

// Exchange the stored refresh token for a fresh access token. Concurrent callers
// share a single in-flight request so we don't fire N refreshes at once. Resolves
// to true on success; clears tokens and resolves false when the refresh is rejected.
let refreshInFlight: Promise<boolean> | null = null;

function refreshOnce(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    const refresh = tokenStore.refresh;
    if (!refresh) return false;
    // Preserve the storage mode chosen at login — a mid-session refresh shouldn't silently
    // move a "remember me" session into sessionStorage or vice versa.
    const remember = window.localStorage.getItem(REFRESH_KEY) !== null;
    try {
      const res = await fetch(`${BASE}/api/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: refresh }),
      });
      if (!res.ok) {
        tokenStore.clear();
        return false;
      }
      const data = (await res.json()) as AuthResponse;
      tokenStore.set(data.accessToken, data.refreshToken, remember);
      return true;
    } catch {
      return false;
    }
  })().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

async function request<T>(path: string, options: RequestInit = {}, retry = true): Promise<T> {
  const headers = new Headers(options.headers);
  if (!(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
  const token = tokenStore.access;
  if (token) headers.set("Authorization", `Bearer ${token}`);

  let res: Response;
  try {
    res = await fetch(`${BASE}${path}`, { ...options, headers });
  } catch {
    throw new Error("NETWORK_ERROR");
  }

  // Access token likely expired — refresh once and replay the request.
  if (res.status === 401 && retry && token && (await refreshOnce())) {
    return request<T>(path, options, false);
  }

  // Request body too large (size limit) — the response has no JSON body; surface a stable code the
  // UI can localize instead of the raw "Payload Too Large" status text.
  if (res.status === 413) throw new Error("UPLOAD_TOO_LARGE");

  // Rate limiter rejection (Program.cs) has no JSON body either — same stable-code treatment.
  if (res.status === 429) throw new Error("RATE_LIMITED");

  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? "Request failed");
  }
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

export const api = {
  // --- Auth ---
  register: (body: {
    email: string; password: string; displayName: string; preferredLanguage: number; organizationSlug?: string | null;
  }) => request<{ message: string; email: string }>("/api/auth/register", { method: "POST", body: JSON.stringify(body) }),
  login: (body: { email: string; password: string }) =>
    request<AuthResponse>("/api/auth/login", { method: "POST", body: JSON.stringify(body) }),
  forgotPassword: (email: string) =>
    request("/api/auth/forgot-password", { method: "POST", body: JSON.stringify({ email }) }),
  resetPassword: (email: string, resetToken: string, newPassword: string) =>
    request<{ message: string }>("/api/auth/reset-password", {
      method: "POST", body: JSON.stringify({ email, resetToken, newPassword }),
    }),
  verifyEmail: (email: string, token: string) =>
    request<AuthResponse>("/api/auth/verify-email", { method: "POST", body: JSON.stringify({ email, token }) }),
  resendVerification: (email: string) =>
    request<{ message: string }>("/api/auth/resend-verification", { method: "POST", body: JSON.stringify({ email }) }),

  // --- User ---
  me: () => request<AuthUser>("/api/users/me"),
  // --- Account (GDPR): export everything the account owns, or delete it permanently ---
  exportAccount: () => request<AccountExport>("/api/users/me/export"),
  deleteAccount: () => request<void>("/api/users/me", { method: "DELETE" }),

  // --- Documents ---
  listDocuments: () => request<DocumentSummary[]>("/api/documents"),
  getDocument: (id: string) => request<DocumentDetail>(`/api/documents/${id}`),
  deleteDocument: (id: string) => request<void>(`/api/documents/${id}`, { method: "DELETE" }),
  // Accepts one or more pages; order is preserved end-to-end (browser → Request.Form.Files → binding).
  uploadDocument: (files: File[]) => {
    const form = new FormData();
    files.forEach((f) => form.append("files", f));
    return request<UploadDocumentResult>("/api/documents", { method: "POST", body: form });
  },
  // Generic analysis — the AI auto-detects the document type (bank, government, etc.).
  analyze: (id: string) =>
    request<AnalysisResult>(`/api/documents/${id}/analyze`, { method: "POST" }),

  // --- Anonymous trial (no auth, nothing saved) ---
  trialAnalyze: (files: File[]) => {
    const form = new FormData();
    files.forEach((f) => form.append("files", f));
    return request<AnalysisResult>("/api/trial/analyze", { method: "POST", body: form });
  },
  trialSpeech: async (analysis: AnalysisResult, language: number, section?: number): Promise<Blob> => {
    const res = await fetch(`${BASE}/api/trial/speech`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ analysis, language, section }),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: res.statusText }));
      throw new Error(err.error ?? "Speech synthesis failed");
    }
    return res.blob();
  },

  // --- Text to speech (returns an MP3 blob) ---
  speech: async (id: string, language: number, section?: number): Promise<Blob> => {
    const url = `${BASE}/api/documents/${id}/speech?language=${language}${section ? `&section=${section}` : ""}`;
    const send = () => {
      const headers = new Headers();
      const token = tokenStore.access;
      if (token) headers.set("Authorization", `Bearer ${token}`);
      return fetch(url, { headers });
    };
    let res = await send();
    if (res.status === 401 && (await refreshOnce())) res = await send();
    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: res.statusText }));
      throw new Error(err.error ?? "Speech synthesis failed");
    }
    return res.blob();
  },

  // Fetch one uploaded page's raw file (image/PDF) so the user can see what they photographed.
  // <img>/<a> can't carry a bearer token, so this returns a blob for the caller to turn into
  // an object URL — same pattern as speech() above.
  documentPage: async (id: string, index: number): Promise<Blob> => {
    const url = `${BASE}/api/documents/${id}/pages/${index}`;
    const send = () => {
      const headers = new Headers();
      const token = tokenStore.access;
      if (token) headers.set("Authorization", `Bearer ${token}`);
      return fetch(url, { headers });
    };
    let res = await send();
    if (res.status === 401 && (await refreshOnce())) res = await send();
    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: res.statusText }));
      throw new Error(err.error ?? "Failed to load page");
    }
    return res.blob();
  },

  // --- Sponsored referrals (no auth required — works for trial users too) ---
  getReferrals: (category: number) =>
    request<Provider[]>(`/api/referrals?category=${category}`),
  logLead: (providerId: string, body: { category: number; urgency: number; documentId?: string; ref?: string }) =>
    request<{ ok: boolean }>(`/api/referrals/${providerId}/lead`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  submitLeadFeedback: (ref: string, helpful: boolean) =>
    request<{ ok: boolean }>(`/api/referrals/feedback/${ref}`, {
      method: "POST",
      body: JSON.stringify({ helpful }),
    }),

  // --- Business self-registration (public) ---
  applyAsProvider: (body: ProviderApplication) =>
    request<{ ok: boolean }>("/api/partners/apply", { method: "POST", body: JSON.stringify(body) }),

  // --- Admin: provider review (requires admin account) ---
  adminListPending: () => request<PendingProvider[]>("/api/admin/providers/pending"),
  adminApprove: (id: string) =>
    request<{ ok: boolean }>(`/api/admin/providers/${id}/approve`, { method: "POST" }),
  adminReject: (id: string) =>
    request<void>(`/api/admin/providers/${id}`, { method: "DELETE" }),

  // --- Admin: manage live/reviewed providers ---
  adminListProviders: () => request<ManagedProvider[]>("/api/admin/providers"),
  adminUpdateProvider: (id: string, body: UpdateProvider) =>
    request<{ ok: boolean }>(`/api/admin/providers/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  adminSetActive: (id: string, value: boolean) =>
    request<{ ok: boolean }>(`/api/admin/providers/${id}/active?value=${value}`, { method: "POST" }),
  adminLeads: () => request<LeadsOverview>("/api/admin/leads"),
  adminUpdateLeadStatus: (id: string, status: number) =>
    request<{ ok: boolean }>(`/api/admin/leads/${id}/status`, {
      method: "PUT",
      body: JSON.stringify({ status }),
    }),

  // --- Admin: provider invoicing (persisted + emailed PDF invoices) ---
  // No invoice yet for this provider+month -> the API's Ok(null) is auto-converted to a
  // 204 by ASP.NET Core, which request() surfaces as undefined, not null — callers should
  // treat both as "no invoice" (e.g. `inv ?? null`).
  adminInvoiceStatus: (providerId: string, year: number, month: number) =>
    request<Invoice | null | undefined>(`/api/admin/providers/${providerId}/invoices/status?year=${year}&month=${month}`),
  adminGenerateInvoice: (providerId: string, year: number, month: number) =>
    request<Invoice>(`/api/admin/providers/${providerId}/invoices/generate`, {
      method: "POST", body: JSON.stringify({ year, month }),
    }),
  // Invoice PDFs are behind [Authorize], so a plain <a href> can't fetch them (no way to attach
  // the bearer token) — download as a blob with the same auth+refresh pattern as speech().
  adminInvoicePdf: async (providerId: string, invoiceId: string): Promise<Blob> => {
    const url = `${BASE}/api/admin/providers/${providerId}/invoices/${invoiceId}/pdf`;
    const send = () => {
      const headers = new Headers();
      const token = tokenStore.access;
      if (token) headers.set("Authorization", `Bearer ${token}`);
      return fetch(url, { headers });
    };
    let res = await send();
    if (res.status === 401 && (await refreshOnce())) res = await send();
    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: res.statusText }));
      throw new Error(err.error ?? "Failed to fetch invoice PDF");
    }
    return res.blob();
  },

  // --- Organizations (B2B/B2G tenant branding — public, anonymous) ---
  getOrganization: (slug: string) => request<OrganizationBranding>(`/api/organizations/${slug}`),

  // --- Admin: B2B/B2G tenant management ---
  adminListOrganizations: () => request<OrganizationSummary[]>("/api/admin/organizations"),
  adminCreateOrganization: (body: CreateOrganizationPayload) =>
    request<{ id: string }>("/api/admin/organizations", { method: "POST", body: JSON.stringify(body) }),
  adminUpdateOrganization: (id: string, body: UpdateOrganizationPayload) =>
    request<{ ok: boolean }>(`/api/admin/organizations/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  adminSetOrganizationStatus: (id: string, value: boolean) =>
    request<{ ok: boolean }>(`/api/admin/organizations/${id}/active?value=${value}`, { method: "POST" }),
  adminOrganizationStats: (id: string) => request<OrganizationStats>(`/api/admin/organizations/${id}/stats`),

  // --- Admin: product funnel (event counts over a trailing window) ---
  adminFunnel: (days: number) => request<Funnel>(`/api/admin/analytics/funnel?days=${days}`),
  adminFunnelEvents: (eventName: string, days: number) =>
    request<EventDetail[]>(`/api/admin/analytics/funnel/${eventName}?days=${days}`),

  // --- Chat ---
  chatHistory: (id: string) => request<ChatMessage[]>(`/api/documents/${id}/chat`),
  sendChat: (id: string, question: string, responseLanguage: number) =>
    request<ChatMessage>(`/api/documents/${id}/chat`, {
      method: "POST",
      body: JSON.stringify({ question, responseLanguage }),
    }),
};
