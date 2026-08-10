// Shared by AnalysisVerdict (the "Pay by ... · in N days" line) and AnalysisCard (the
// Deadlines section's auto-expand rule) — both need the same "how many whole days away" figure.

function startOfDay(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}

/** Whole calendar days between today and an ISO date string — negative if already past. */
export function daysUntil(dateIso: string): number {
  return Math.round((startOfDay(new Date(dateIso)) - startOfDay(new Date())) / 86_400_000);
}
