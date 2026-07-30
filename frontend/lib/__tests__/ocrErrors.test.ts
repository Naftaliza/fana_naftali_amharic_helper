import { mapOcrError } from "@/lib/ocrErrors";

const t = (key: string) => ({
  "doc.ocrFailedUnreadable": "We couldn't read any text in this document.",
  "doc.ocrFailedService": "Something went wrong on our end.",
  "doc.ocrFailed": "We couldn't read this document.",
}[key] ?? key);

describe("mapOcrError", () => {
  it("maps the blank/unreadable-page message to a plain-language cause", () => {
    expect(mapOcrError("No readable text was found in the document.", t))
      .toBe("We couldn't read any text in this document.");
  });

  it("maps a configuration/API-key error to a generic service message, not a blur/lighting hint", () => {
    expect(mapOcrError("Missing Anthropic API key configuration.", t))
      .toBe("Something went wrong on our end.");
  });

  it("maps an account-level service outage (quota/rate-limit) to the same generic service message, not the unreadable-document hint", () => {
    expect(mapOcrError("OCR service is temporarily unavailable (429).", t))
      .toBe("Something went wrong on our end.");
  });

  it("falls back to the generic ocrFailed message for anything unrecognized", () => {
    expect(mapOcrError("Some new backend error string", t)).toBe("We couldn't read this document.");
  });

  it("falls back to the generic message when there is no error at all", () => {
    expect(mapOcrError(null, t)).toBe("We couldn't read this document.");
  });
});
