"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api } from "@/lib/api";
import type { OrganizationBranding } from "@/lib/types";

const STORAGE_KEY = "orgSlug";

interface OrganizationContextValue {
  organization: OrganizationBranding | null;
  loading: boolean;
}

const OrganizationContext = createContext<OrganizationContextValue | null>(null);

/**
 * Resolves the B2B/B2G tenant (if any) this browser is visiting under, and exposes its
 * branding. A tenant-branded entry point links in with `?org=slug`; that slug is persisted
 * to localStorage so it survives client-side navigation (Next.js <Link> won't carry the
 * query param). Consumer visitors never set this, so `organization` stays null and nothing
 * about the default experience changes.
 */
export function OrganizationProvider({ children }: { children: ReactNode }) {
  const [organization, setOrganization] = useState<OrganizationBranding | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fromUrl = new URLSearchParams(window.location.search).get("org");
    const slug = fromUrl ?? window.localStorage.getItem(STORAGE_KEY);
    if (!slug) {
      setLoading(false);
      return;
    }
    if (fromUrl) window.localStorage.setItem(STORAGE_KEY, fromUrl);

    api
      .getOrganization(slug)
      .then((org) => {
        setOrganization(org);
        // Expose the tenant's colors as CSS custom properties. Only elements that opt in
        // via var(--org-primary, ...) pick these up — Fana's own chrome is untouched, so a
        // consumer visitor's experience never changes.
        document.documentElement.style.setProperty("--org-primary", org.primaryColorHex);
        document.documentElement.style.setProperty("--org-accent", org.accentColorHex ?? org.primaryColorHex);
      })
      .catch(() => {
        // Unknown or deactivated slug — stop treating this browser as tenant-branded.
        window.localStorage.removeItem(STORAGE_KEY);
      })
      .finally(() => setLoading(false));
  }, []);

  return (
    <OrganizationContext.Provider value={{ organization, loading }}>
      {children}
    </OrganizationContext.Provider>
  );
}

export function useOrganization() {
  const ctx = useContext(OrganizationContext);
  if (!ctx) throw new Error("useOrganization must be used within OrganizationProvider");
  return ctx;
}

/**
 * The persisted tenant slug, read synchronously (no async branding fetch needed). Use this
 * as a fallback wherever a slug is needed immediately — e.g. on submit, a user can act
 * before OrganizationProvider's branding fetch resolves, and `organization` would still be
 * null even though the tenant is known.
 */
export function getPersistedOrgSlug(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(STORAGE_KEY);
}
