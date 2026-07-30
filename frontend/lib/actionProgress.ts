// Persists which required actions the user has checked off, per document — same localStorage-flag
// approach as lib/onboarding.ts/lib/trial.ts, keyed per document like lib/leadFeedback.ts's blob.
// Actions have no stable id from the backend, so they're tracked by their position in
// analysis.requiredActions; re-running analysis on the same document resets progress since the
// action list itself may have changed.

const keyFor = (documentId: string) => `actionProgress:${documentId}`;

export function getCheckedActions(documentId: string): number[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = window.localStorage.getItem(keyFor(documentId));
    const parsed = JSON.parse(raw ?? "[]");
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function isActionChecked(documentId: string, index: number): boolean {
  return getCheckedActions(documentId).includes(index);
}

export function setActionChecked(documentId: string, index: number, checked: boolean): void {
  const current = new Set(getCheckedActions(documentId));
  if (checked) current.add(index);
  else current.delete(index);
  window.localStorage.setItem(keyFor(documentId), JSON.stringify([...current].sort((a, b) => a - b)));
}
