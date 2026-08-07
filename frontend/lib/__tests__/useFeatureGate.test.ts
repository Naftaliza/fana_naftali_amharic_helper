import { renderHook } from "@testing-library/react";
import { useFeatureGate } from "@/lib/useFeatureGate";

const replace = jest.fn();
jest.mock("next/navigation", () => ({ useRouter: () => ({ replace: (...args: unknown[]) => replace(...args) }) }));

describe("useFeatureGate", () => {
  beforeEach(() => replace.mockClear());

  it("redirects home when the flag is disabled", () => {
    const { result } = renderHook(() => useFeatureGate(false));
    expect(replace).toHaveBeenCalledWith("/");
    expect(result.current).toBe(false);
  });

  it("does not redirect when the flag is enabled", () => {
    const { result } = renderHook(() => useFeatureGate(true));
    expect(replace).not.toHaveBeenCalled();
    expect(result.current).toBe(true);
  });
});
