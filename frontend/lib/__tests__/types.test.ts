import { loc, type LocalizedText } from "@/lib/types";

// Plain ASCII markers stand in for real language content here — the fallback logic under test
// doesn't care what the text actually says, and ASCII avoids any Unicode-normalization pitfalls
// between two typed occurrences of the same non-Latin characters.
describe("loc", () => {
  const text: LocalizedText = { he: "HE_TEXT", am: "AM_TEXT", en: "EN_TEXT" };

  it("returns the exact-language text when present", () => {
    expect(loc(text, "he")).toBe("HE_TEXT");
    expect(loc(text, "am")).toBe("AM_TEXT");
    expect(loc(text, "en")).toBe("EN_TEXT");
  });

  it("falls back through the documented order when the requested language is blank", () => {
    const amBlank: LocalizedText = { he: "HE_TEXT", am: "", en: "EN_TEXT" };
    // am -> am, en, he
    expect(loc(amBlank, "am")).toBe("EN_TEXT");

    const enBlank: LocalizedText = { he: "HE_TEXT", am: "AM_TEXT", en: "" };
    // en -> en, he, am
    expect(loc(enBlank, "en")).toBe("HE_TEXT");

    const heBlank: LocalizedText = { he: "", am: "AM_TEXT", en: "EN_TEXT" };
    // he -> he, en, am
    expect(loc(heBlank, "he")).toBe("EN_TEXT");
  });

  it("falls all the way through when only the last-priority language has text", () => {
    const onlyAm: LocalizedText = { he: "", am: "AM_TEXT", en: "" };
    // en -> en, he, am
    expect(loc(onlyAm, "en")).toBe("AM_TEXT");
  });

  it("returns an empty string when every slot is blank", () => {
    const allBlank: LocalizedText = { he: "", am: "", en: "" };
    expect(loc(allBlank, "he")).toBe("");
  });

  it("returns an empty string for undefined input", () => {
    expect(loc(undefined, "he")).toBe("");
  });
});
