// Builds a minimal RFC 5545 .ics blob for a single all-day event, client-side, so "Add to
// calendar" needs no backend and no calendar-API permissions — it just hands the phone's own
// calendar app a file, which then owns reminders.

function pad(n: number): string {
  return String(n).padStart(2, "0");
}

function dateStamp(d: Date): string {
  return `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}`;
}

function escapeText(s: string): string {
  return s.replace(/\\/g, "\\\\").replace(/,/g, "\\,").replace(/;/g, "\\;").replace(/\n/g, "\\n");
}

export function buildDeadlineIcs({
  date,
  description,
  documentName,
}: {
  date: string;
  description: string;
  documentName: string;
}): Blob {
  const start = new Date(date);
  const end = new Date(start);
  end.setUTCDate(end.getUTCDate() + 1);

  const uid = `${dateStamp(start)}-${Math.random().toString(36).slice(2)}@fana.app`;
  const summary = escapeText(`${description} (${documentName})`);

  const lines = [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//Fana//Document Deadline//EN",
    "BEGIN:VEVENT",
    `UID:${uid}`,
    `DTSTAMP:${dateStamp(new Date())}T000000Z`,
    `DTSTART;VALUE=DATE:${dateStamp(start)}`,
    `DTEND;VALUE=DATE:${dateStamp(end)}`,
    `SUMMARY:${summary}`,
    "END:VEVENT",
    "END:VCALENDAR",
  ];

  return new Blob([lines.join("\r\n")], { type: "text/calendar" });
}
