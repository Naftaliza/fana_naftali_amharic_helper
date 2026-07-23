import { api, tokenStore } from "@/lib/api";

// request() is a private module function, but its full behavior is exercised through any
// public api.* method — api.me() (a plain GET) is used throughout as the vehicle, matching
// the "no mocking library, exercise the real thing" spirit applied to the one place a real
// network boundary must be faked (global.fetch itself, via Jest's built-in jest.fn()).

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as unknown as Response;
}

function emptyResponse(status: number): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => { throw new Error("no body"); },
  } as unknown as Response;
}

describe("api request()", () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    global.fetch = jest.fn();
  });

  it("refreshes once and replays the original request on a 401", async () => {
    tokenStore.set("expired-access-token", "valid-refresh-token", true);

    const fetchMock = global.fetch as jest.Mock;
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401, { error: "Unauthorized" })) // original call
      .mockResolvedValueOnce(jsonResponse(200, { // /api/auth/refresh
        accessToken: "new-access-token",
        refreshToken: "new-refresh-token",
        user: { id: "1", email: "user@test.local", displayName: "User", preferredLanguage: 0 },
      }))
      .mockResolvedValueOnce(jsonResponse(200, { // replayed original call
        id: "1", email: "user@test.local", displayName: "User", preferredLanguage: 0,
      }));

    const result = await api.me();

    expect(result.email).toBe("user@test.local");
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(tokenStore.access).toBe("new-access-token");
  });

  it("resolves to undefined for a 204 No Content response", async () => {
    tokenStore.set("access-token", "refresh-token", true);
    (global.fetch as jest.Mock).mockResolvedValueOnce(emptyResponse(204));

    const result = await api.me();

    expect(result).toBeUndefined();
  });

  it("throws a stable UPLOAD_TOO_LARGE error for a 413 response", async () => {
    tokenStore.set("access-token", "refresh-token", true);
    (global.fetch as jest.Mock).mockResolvedValueOnce(emptyResponse(413));

    await expect(api.me()).rejects.toThrow("UPLOAD_TOO_LARGE");
  });

  it("throws the server's error message for a generic failure", async () => {
    tokenStore.set("access-token", "refresh-token", true);
    (global.fetch as jest.Mock).mockResolvedValueOnce(jsonResponse(500, { error: "Something broke" }));

    await expect(api.me()).rejects.toThrow("Something broke");
  });

  it("throws a stable RATE_LIMITED error for a 429 response", async () => {
    tokenStore.set("access-token", "refresh-token", true);
    (global.fetch as jest.Mock).mockResolvedValueOnce(emptyResponse(429));

    await expect(api.me()).rejects.toThrow("RATE_LIMITED");
  });

  it("throws a stable NETWORK_ERROR when fetch itself fails", async () => {
    tokenStore.set("access-token", "refresh-token", true);
    (global.fetch as jest.Mock).mockRejectedValueOnce(new TypeError("Failed to fetch"));

    await expect(api.me()).rejects.toThrow("NETWORK_ERROR");
  });
});

describe("tokenStore remember-me storage", () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it("persists to localStorage and clears sessionStorage when remember is true", () => {
    window.sessionStorage.setItem("accessToken", "stale-session-token");
    tokenStore.set("access-token", "refresh-token", true);

    expect(window.localStorage.getItem("accessToken")).toBe("access-token");
    expect(window.sessionStorage.getItem("accessToken")).toBeNull();
    expect(tokenStore.access).toBe("access-token");
  });

  it("persists to sessionStorage and clears localStorage when remember is false", () => {
    window.localStorage.setItem("accessToken", "stale-local-token");
    tokenStore.set("access-token", "refresh-token", false);

    expect(window.sessionStorage.getItem("accessToken")).toBe("access-token");
    expect(window.localStorage.getItem("accessToken")).toBeNull();
    expect(tokenStore.access).toBe("access-token");
  });

  it("clears both storages on clear()", () => {
    tokenStore.set("access-token", "refresh-token", true);
    tokenStore.clear();

    expect(window.localStorage.getItem("accessToken")).toBeNull();
    expect(window.sessionStorage.getItem("accessToken")).toBeNull();
    expect(tokenStore.access).toBeNull();
  });
});
