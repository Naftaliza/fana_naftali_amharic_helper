import { renderHook, act } from "@testing-library/react";
import { useOnlineStatus } from "@/lib/useOnlineStatus";

describe("useOnlineStatus", () => {
  afterEach(() => {
    Object.defineProperty(window.navigator, "onLine", { value: true, configurable: true });
  });

  it("reflects navigator.onLine on first render", () => {
    Object.defineProperty(window.navigator, "onLine", { value: false, configurable: true });
    const { result } = renderHook(() => useOnlineStatus());
    expect(result.current).toBe(false);
  });

  it("flips to false when the offline event fires", () => {
    const { result } = renderHook(() => useOnlineStatus());
    expect(result.current).toBe(true);
    act(() => window.dispatchEvent(new Event("offline")));
    expect(result.current).toBe(false);
  });

  it("flips back to true when the online event fires", () => {
    const { result } = renderHook(() => useOnlineStatus());
    act(() => window.dispatchEvent(new Event("offline")));
    act(() => window.dispatchEvent(new Event("online")));
    expect(result.current).toBe(true);
  });
});
