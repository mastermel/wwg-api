import { http, HttpResponse } from "msw";
import type { MeResponse } from "@/api/generated/model";
import { server } from "@/test/server";

export const testUser: MeResponse = {
  id: "0192f5c1-0000-7000-8000-000000000001",
  email: "mel@example.com",
  firstName: "Mel",
  lastName: "Green",
  isAdmin: false,
  masquerade: null,
};

export const testAdmin: MeResponse = {
  id: "0192f5c1-0000-7000-8000-0000000000ad",
  email: "admin@example.com",
  firstName: "Ada",
  lastName: "Admin",
  isAdmin: true,
  masquerade: null,
};

/**
 * Mocks the session endpoints: a valid refresh cookie for `user` ("signed-in"), none
 * ("signed-out"), or the API unreachable ("offline"). Returns counters for the calls made.
 */
export function mockSession(
  mode: "signed-in" | "signed-out" | "offline",
  user: MeResponse = testUser,
) {
  const calls = { refresh: 0, logout: 0 };
  server.use(
    http.post("*/api/auth/refresh", () => {
      calls.refresh++;
      if (mode === "offline") return HttpResponse.error();
      return mode === "signed-in"
        ? HttpResponse.json({ accessToken: `token-for-${user.id}`, expiresIn: 1800 })
        : new HttpResponse(null, { status: 401 });
    }),
    http.get("*/api/me", ({ request }) =>
      request.headers.get("Authorization") === `Bearer token-for-${user.id}`
        ? HttpResponse.json(user)
        : new HttpResponse(null, { status: 401 }),
    ),
    http.post("*/api/auth/logout", () => {
      calls.logout++;
      return mode === "offline" ? HttpResponse.error() : new HttpResponse(null, { status: 204 });
    }),
  );
  return calls;
}
