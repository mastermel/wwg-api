import { onlineManager } from "@tanstack/react-query";
import { http, HttpResponse } from "msw";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createQueryClient } from "@/app/query-client";
import { createSessionStore, type SessionStore } from "@/features/auth/session-store";
import { getAccessToken, refreshAccessToken } from "@/lib/access-token";
import { server } from "@/test/server";
import { mockSession, testUser } from "@/test/session";

const stores: SessionStore[] = [];

function newStore() {
  const queryClient = createQueryClient();
  const clearSavedData = vi.fn(() => Promise.resolve());
  const store = createSessionStore({ queryClient, clearSavedData });
  stores.push(store);
  return { store, queryClient, clearSavedData };
}

describe("session store", () => {
  afterEach(() => {
    for (const store of stores.splice(0)) store.dispose();
    localStorage.clear();
  });

  it("starts signed in with the user when the refresh cookie is valid", async () => {
    mockSession("signed-in");
    const { store } = newStore();

    await store.start();

    expect(store.getState()).toEqual({ status: "signed-in", user: testUser, ended: false });
    expect(JSON.parse(localStorage.getItem("wwg:last-user") ?? "null")).toEqual(testUser);
  });

  it("starts signed out, and forgets any saved data, when there's no session", async () => {
    mockSession("signed-out");
    localStorage.setItem("wwg:last-user", JSON.stringify(testUser));
    const { store, clearSavedData } = newStore();

    await store.start();

    expect(store.getState().status).toBe("signed-out");
    expect(localStorage.getItem("wwg:last-user")).toBeNull();
    expect(clearSavedData).toHaveBeenCalled();
  });

  it("starts offline as the last user when the API can't be reached", async () => {
    mockSession("offline");
    localStorage.setItem("wwg:last-user", JSON.stringify(testUser));
    const { store } = newStore();

    await store.start();

    expect(store.getState()).toEqual({ status: "offline", user: testUser, ended: false });
  });

  it("starts signed out when offline with no last user", async () => {
    mockSession("offline");
    const { store } = newStore();

    await store.start();

    expect(store.getState().status).toBe("signed-out");
  });

  it("ignores a last-user record with the wrong shape", async () => {
    mockSession("offline");
    localStorage.setItem("wwg:last-user", JSON.stringify({ id: 42 }));
    const { store } = newStore();

    await store.start();

    expect(store.getState().status).toBe("signed-out");
  });

  it("finishes signing in once back online", async () => {
    mockSession("offline");
    localStorage.setItem("wwg:last-user", JSON.stringify(testUser));
    const { store } = newStore();
    await store.start();

    mockSession("signed-in");
    onlineManager.setOnline(false);
    onlineManager.setOnline(true);

    await vi.waitFor(() => {
      expect(store.getState().status).toBe("signed-in");
    });
  });

  it("clears the previous user's data when someone else signs in here", async () => {
    localStorage.setItem(
      "wwg:last-user",
      JSON.stringify({ ...testUser, id: "0192f5c1-0000-7000-8000-00000000000a" }),
    );
    const other = { ...testUser, id: "0192f5c1-0000-7000-8000-000000000002" };
    mockSession("signed-in", other);
    const { store, queryClient, clearSavedData } = newStore();
    queryClient.setQueryData(["campaigns"], ["the other user's campaign"]);

    await store.signIn({ accessToken: `token-for-${other.id}`, expiresIn: 1800 });

    expect(store.getState().user?.id).toBe(other.id);
    expect(queryClient.getQueryData(["campaigns"])).toBeUndefined();
    expect(clearSavedData).toHaveBeenCalled();
  });

  it("signs out: tells the API, and forgets the user and their data", async () => {
    const calls = mockSession("signed-in");
    const { store, clearSavedData } = newStore();
    await store.start();

    await store.signOut();

    expect(calls.logout).toBe(1);
    expect(store.getState().status).toBe("signed-out");
    expect(localStorage.getItem("wwg:last-user")).toBeNull();
    expect(clearSavedData).toHaveBeenCalled();
  });

  it("stays signed out after signing out offline, and finishes it at the next start", async () => {
    mockSession("signed-in");
    const first = newStore().store;
    await first.start();
    mockSession("offline");
    await first.signOut();

    // Back online, the refresh cookie still works: the next start must not use it.
    const calls = mockSession("signed-in");
    const next = newStore().store;
    await next.start();

    expect(next.getState().status).toBe("signed-out");
    expect(calls.logout).toBe(1);
    expect(calls.refresh).toBe(0);
    expect(localStorage.getItem("wwg:sign-out-pending")).toBeNull();
  });

  it("marks the session as ended when a refresh finds it's over", async () => {
    mockSession("signed-in");
    const { store } = newStore();
    await store.start();

    mockSession("signed-out");
    await refreshAccessToken();

    await vi.waitFor(() => {
      expect(store.getState()).toMatchObject({ status: "signed-out", ended: true });
    });
  });

  it("signs out other tabs, and signs them in", async () => {
    mockSession("signed-in");
    const tab1 = newStore().store;
    const tab2 = newStore().store;
    await tab1.start();
    await tab2.start();

    await tab1.signOut();
    await vi.waitFor(() => {
      expect(tab2.getState().status).toBe("signed-out");
    });

    await tab1.signIn({ accessToken: `token-for-${testUser.id}`, expiresIn: 1800 });
    await vi.waitFor(() => {
      expect(tab2.getState().status).toBe("signed-in");
    });
  });

  it("starts offline, not stuck, when the refresh answers 200 with something other than a token", async () => {
    mockSession("signed-in");
    server.use(
      http.post("*/api/auth/refresh", () => HttpResponse.html("<p>Sign in to the Wi-Fi</p>")),
    );
    localStorage.setItem("wwg:last-user", JSON.stringify(testUser));
    const { store } = newStore();

    await store.start();
    await store.ready;

    expect(store.getState().status).toBe("offline");
    expect(getAccessToken()).toBeNull();
  });

  it("still signs out when the saved data can't be deleted", async () => {
    mockSession("signed-out");
    const store = createSessionStore({
      queryClient: createQueryClient(),
      clearSavedData: () => Promise.reject(new Error("IndexedDB is unavailable")),
    });
    stores.push(store);

    await store.start();
    await store.ready;

    expect(store.getState().status).toBe("signed-out");
  });

  it("fetches the user again after a later refresh", async () => {
    mockSession("signed-in");
    const { store } = newStore();
    await store.start();

    const promoted = { ...testUser, firstName: "Melanie", isAdmin: true };
    mockSession("signed-in", promoted);
    await refreshAccessToken();

    await vi.waitFor(() => {
      expect(store.getState().user).toEqual(promoted);
    });
  });

  it("keeps no token when signing in can't load the user", async () => {
    mockSession("signed-out");
    const { store } = newStore();
    await store.start();
    server.use(http.get("*/api/me", () => HttpResponse.error()));

    await expect(
      store.signIn({ accessToken: `token-for-${testUser.id}`, expiresIn: 1800 }),
    ).rejects.toThrow();

    expect(store.getState().status).toBe("signed-out");
    expect(getAccessToken()).toBeNull();
  });

  it("switches to another user (a masquerade), forgetting everything saved first", async () => {
    mockSession("signed-in");
    const { store, queryClient, clearSavedData } = newStore();
    await store.start();
    queryClient.setQueryData(["/api/campaigns"], ["the Admin's campaigns"]);
    clearSavedData.mockClear();
    const bob = {
      ...testUser,
      id: "0192f5c1-0000-7000-8000-0000000000b0",
      firstName: "Bob",
      masquerade: { adminName: "Mel Green", endsAt: "2026-09-30T20:00:00Z" },
    };
    server.use(
      http.get("*/api/me", ({ request }) =>
        request.headers.get("Authorization") === `Bearer token-for-${bob.id}`
          ? HttpResponse.json(bob)
          : new HttpResponse(null, { status: 401 }),
      ),
    );

    await store.switchUser({ accessToken: `token-for-${bob.id}`, expiresIn: 1800 });

    expect(store.getState()).toEqual({ status: "signed-in", user: bob, ended: false });
    expect(queryClient.getQueryData(["/api/campaigns"])).toBeUndefined();
    expect(clearSavedData).toHaveBeenCalled();
    expect(JSON.parse(localStorage.getItem("wwg:last-user") ?? "null")).toEqual(bob);
  });

  it("reads a last user saved before masquerades existed", async () => {
    mockSession("offline");
    const before: Partial<typeof testUser> = { ...testUser };
    delete before.masquerade;
    localStorage.setItem("wwg:last-user", JSON.stringify(before));
    const { store } = newStore();

    await store.start();

    expect(store.getState()).toEqual({ status: "offline", user: testUser, ended: false });
  });
});
