import { http, HttpResponse } from "msw";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  getAccessToken,
  onSignedOut,
  refreshAccessToken,
  setAccessToken,
} from "@/lib/access-token";
import { apiFetch } from "@/lib/api-fetch";
import { server } from "@/test/server";

function refreshReturns(status: number, token = "fresh-token") {
  const calls = { count: 0 };
  server.use(
    http.post("*/api/auth/refresh", () => {
      calls.count++;
      return status === 200
        ? HttpResponse.json({ accessToken: token, expiresIn: 1800 })
        : new HttpResponse(null, { status });
    }),
  );
  return calls;
}

describe("access token", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("sends the access token with API calls", async () => {
    setAccessToken({ accessToken: "abc", expiresIn: 1800 });
    let authorization: string | null = null;
    server.use(
      http.get("*/api/me", ({ request }) => {
        authorization = request.headers.get("Authorization");
        return HttpResponse.json({});
      }),
    );

    await apiFetch("/api/me", { method: "GET" });

    expect(authorization).toBe("Bearer abc");
  });

  it("refreshes once after a 401 and retries with the new token", async () => {
    setAccessToken({ accessToken: "expired", expiresIn: 1800 });
    refreshReturns(200, "fresh-token");
    server.use(
      http.get("*/api/me", ({ request }) =>
        request.headers.get("Authorization") === "Bearer fresh-token"
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 }),
      ),
    );

    await expect(apiFetch("/api/me", { method: "GET" })).resolves.toEqual({ ok: true });
  });

  it("shares one refresh between concurrent callers", async () => {
    const calls = refreshReturns(200);

    await Promise.all([refreshAccessToken(), refreshAccessToken(), refreshAccessToken()]);

    expect(calls.count).toBe(1);
    expect(getAccessToken()).toBe("fresh-token");
  });

  it("reports signed out, and tells listeners, only when refresh answers 401", async () => {
    refreshReturns(401);
    const listener = vi.fn();
    onSignedOut(listener);

    await expect(refreshAccessToken()).resolves.toBe("signed-out");

    expect(listener).toHaveBeenCalledOnce();
    expect(getAccessToken()).toBeNull();
  });

  it.each([429, 500, 503])(
    "treats a %i from refresh as temporary, not signed out",
    async (status) => {
      refreshReturns(status);
      const listener = vi.fn();
      onSignedOut(listener);

      await expect(refreshAccessToken()).resolves.toBe("unavailable");

      expect(listener).not.toHaveBeenCalled();
    },
  );

  it("treats a network failure during refresh as temporary", async () => {
    server.use(http.post("*/api/auth/refresh", () => HttpResponse.error()));

    await expect(refreshAccessToken()).resolves.toBe("unavailable");
  });

  it("doesn't refresh-and-retry for the auth endpoints' own 401s", async () => {
    const calls = refreshReturns(200);
    server.use(
      http.post("*/api/auth/login", () =>
        HttpResponse.json(
          { title: "Sign-in failed", status: 401 },
          { status: 401, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );

    await expect(apiFetch("/api/auth/login", { method: "POST" })).rejects.toMatchObject({
      status: 401,
    });
    expect(calls.count).toBe(0);
  });

  it("refreshes a minute before the access token expires", async () => {
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    const calls = refreshReturns(200);

    setAccessToken({ accessToken: "abc", expiresIn: 1800 });
    await vi.advanceTimersByTimeAsync((1800 - 61) * 1000);
    expect(calls.count).toBe(0);
    await vi.advanceTimersByTimeAsync(2000);

    expect(calls.count).toBe(1);
  });
});
