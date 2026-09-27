import { onlineManager } from "@tanstack/react-query";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createQueryClient } from "@/app/query-client";
import { createSessionStore, type SessionStore } from "@/features/auth/session-store";
import { refreshAccessToken } from "@/lib/access-token";
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
});
