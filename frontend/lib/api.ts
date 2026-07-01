// Thin fetch wrapper around the ASP.NET Core API with JWT handling.

import type {
  AnalysisResult,
  AuthUser,
  ChatMessage,
  DocumentDetail,
  DocumentSummary,
  LeadsOverview,
  ManagedProvider,
  PendingProvider,
  Provider,
  ProviderApplication,
  UpdateProvider,
  UploadDocumentResult,
} from "@/lib/types";

const BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

const TOKEN_KEY = "accessToken";
const REFRESH_KEY = "refreshToken";

export const tokenStore = {
  get access() {
    return typeof window === "undefined" ? null : window.localStorage.getItem(TOKEN_KEY);
  },
  get refresh() {
    return typeof window === "undefined" ? null : window.localStorage.getItem(REFRESH_KEY);
  },
  set(access: string, refresh: string) {
    window.localStorage.setItem(TOKEN_KEY, access);
    window.localStorage.setItem(REFRESH_KEY, refresh);
  },
  clear() {
    window.localStorage.removeItem(TOKEN_KEY);
    window.localStorage.removeItem(REFRESH_KEY);
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
      tokenStore.set(data.accessToken, data.refreshToken);
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

  const res = await fetch(`${BASE}${path}`, { ...options, headers });

  // Access token likely expired — refresh once and replay the request.
  if (res.status === 401 && retry && token && (await refreshOnce())) {
    return request<T>(path, options, false);
  }

  // Request body too large (size limit) — the response has no JSON body; surface a stable code the
  // UI can localize instead of the raw "Payload Too Large" status text.
  if (res.status === 413) throw new Error("UPLOAD_TOO_LARGE");

  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? "Request failed");
  }
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

export const api = {
  // --- Auth ---
  register: (body: { email: string; password: string; displayName: string; preferredLanguage: number }) =>
    request<AuthResponse>("/api/auth/register", { method: "POST", body: JSON.stringify(body) }),
  login: (body: { email: string; password: string }) =>
    request<AuthResponse>("/api/auth/login", { method: "POST", body: JSON.stringify(body) }),
  forgotPassword: (email: string) =>
    request("/api/auth/forgot-password", { method: "POST", body: JSON.stringify({ email }) }),

  // --- User ---
  me: () => request<AuthUser>("/api/users/me"),

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
  trialSpeech: async (analysis: AnalysisResult, language: number): Promise<Blob> => {
    const res = await fetch(`${BASE}/api/trial/speech`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ analysis, language }),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: res.statusText }));
      throw new Error(err.error ?? "Speech synthesis failed");
    }
    return res.blob();
  },

  // --- Text to speech (returns an MP3 blob) ---
  speech: async (id: string, language: number): Promise<Blob> => {
    const url = `${BASE}/api/documents/${id}/speech?language=${language}`;
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

  // --- Chat ---
  chatHistory: (id: string) => request<ChatMessage[]>(`/api/documents/${id}/chat`),
  sendChat: (id: string, question: string, responseLanguage: number) =>
    request<ChatMessage>(`/api/documents/${id}/chat`, {
      method: "POST",
      body: JSON.stringify({ question, responseLanguage }),
    }),
};
