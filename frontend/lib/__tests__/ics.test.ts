import { buildDeadlineIcs } from "@/lib/ics";

// jsdom's Blob shim doesn't implement .text()/.arrayBuffer() (only jsdom's FileReader supports
// reading a Blob's contents back out) — this helper works around that test-environment gap.
function readBlobText(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsText(blob);
  });
}

describe("buildDeadlineIcs", () => {
  it("produces a VCALENDAR with an all-day VEVENT on the deadline's date", async () => {
    const blob = buildDeadlineIcs({ date: "2026-08-12T00:00:00Z", description: "Pay the fee", documentName: "Tax notice.pdf" });
    const text = await readBlobText(blob);

    expect(text).toContain("BEGIN:VCALENDAR");
    expect(text).toContain("BEGIN:VEVENT");
    expect(text).toContain("DTSTART;VALUE=DATE:20260812");
    expect(text).toContain("DTEND;VALUE=DATE:20260813");
    expect(text).toContain("SUMMARY:Pay the fee (Tax notice.pdf)");
    expect(text).toContain("END:VEVENT");
    expect(text).toContain("END:VCALENDAR");
  });

  it("uses CRLF line endings per RFC 5545", async () => {
    const blob = buildDeadlineIcs({ date: "2026-08-12T00:00:00Z", description: "Pay", documentName: "x.pdf" });
    const text = await readBlobText(blob);
    expect(text).toContain("\r\n");
  });

  it("has a text/calendar MIME type", () => {
    const blob = buildDeadlineIcs({ date: "2026-08-12T00:00:00Z", description: "Pay", documentName: "x.pdf" });
    expect(blob.type).toBe("text/calendar");
  });

  it("escapes commas and semicolons in the summary per RFC 5545", async () => {
    const blob = buildDeadlineIcs({ date: "2026-08-12T00:00:00Z", description: "Pay, or appeal; see notice", documentName: "x.pdf" });
    const text = await readBlobText(blob);
    expect(text).toContain("Pay\\, or appeal\\; see notice");
  });
});
