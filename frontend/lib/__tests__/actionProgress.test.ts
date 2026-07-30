import { getCheckedActions, setActionChecked, isActionChecked } from "@/lib/actionProgress";

describe("actionProgress", () => {
  beforeEach(() => window.localStorage.clear());

  it("returns no checked actions for a document that's never been touched", () => {
    expect(getCheckedActions("doc-1")).toEqual([]);
    expect(isActionChecked("doc-1", 0)).toBe(false);
  });

  it("persists a checked action index for one document", () => {
    setActionChecked("doc-1", 2, true);
    expect(isActionChecked("doc-1", 2)).toBe(true);
    expect(getCheckedActions("doc-1")).toEqual([2]);
  });

  it("unchecking removes the index", () => {
    setActionChecked("doc-1", 2, true);
    setActionChecked("doc-1", 2, false);
    expect(isActionChecked("doc-1", 2)).toBe(false);
    expect(getCheckedActions("doc-1")).toEqual([]);
  });

  it("keeps different documents' progress independent", () => {
    setActionChecked("doc-1", 0, true);
    setActionChecked("doc-2", 0, true);
    setActionChecked("doc-1", 0, false);
    expect(isActionChecked("doc-1", 0)).toBe(false);
    expect(isActionChecked("doc-2", 0)).toBe(true);
  });

  it("tolerates corrupted storage instead of throwing", () => {
    window.localStorage.setItem("actionProgress:doc-1", "{not json");
    expect(getCheckedActions("doc-1")).toEqual([]);
  });
});
