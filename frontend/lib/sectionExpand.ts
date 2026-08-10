// Persists which collapsible result sections (KeyPoints/Actions/Deadlines) a user has manually
// expanded or collapsed, per document — same localStorage-per-document approach as
// lib/actionProgress.ts. Only manual overrides are stored; a section with no stored override
// falls back to its auto-expand rule (see AnalysisCard) every time the page is opened, so a
// mandatory action or a near deadline is never silently hidden just because of a stale toggle
// from a previous visit.

export type CollapsibleSection = "KeyPoints" | "Actions" | "Deadlines";

const keyFor = (documentId: string) => `sectionExpand:${documentId}`;

function getOverrides(documentId: string): Partial<Record<CollapsibleSection, boolean>> {
  if (typeof window === "undefined") return {};
  try {
    const raw = window.localStorage.getItem(keyFor(documentId));
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

export function getSectionOverride(documentId: string, section: CollapsibleSection): boolean | undefined {
  return getOverrides(documentId)[section];
}

export function setSectionOverride(documentId: string, section: CollapsibleSection, expanded: boolean): void {
  const next = { ...getOverrides(documentId), [section]: expanded };
  window.localStorage.setItem(keyFor(documentId), JSON.stringify(next));
}
