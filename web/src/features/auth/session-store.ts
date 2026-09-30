import { onlineManager, type QueryClient } from "@tanstack/react-query";
import { getMe } from "@/api/generated/endpoints/account/account";
import { logout } from "@/api/generated/endpoints/auth/auth";
import type { MeResponse, TokenResponse } from "@/api/generated/model";
import { GetMeResponse } from "@/api/generated/zod/account/account.zod";
import {
  clearAccessToken,
  onRefreshed,
  onSignedOut,
  refreshAccessToken,
  setAccessToken,
} from "@/lib/access-token";

/**
 * - starting: checking the session (a refresh) at start-up.
 * - signed-in: online with a session.
 * - offline: couldn't reach the API; showing the last signed-in user's saved data, read-only.
 * - signed-out: no session.
 */
export type SessionStatus = "starting" | "signed-in" | "offline" | "signed-out";

export interface SessionState {
  status: SessionStatus;
  user: MeResponse | null;
  /** Signed out because the session ended (e.g. a password change elsewhere), not by choice. */
  ended: boolean;
}

export interface SessionStore {
  getState: () => SessionState;
  subscribe: (listener: () => void) => () => void;
  /** Resolves once start-up has settled the first status. */
  ready: Promise<void>;
  start: () => Promise<void>;
  signIn: (token: TokenResponse) => Promise<void>;
  /**
   * Starting or ending a masquerade (decision 0012): the session becomes another user's. Everything
   * saved (here and in other tabs) is cleared first, so nothing crosses between the two.
   */
  switchUser: (token: TokenResponse) => Promise<void>;
  signOut: () => Promise<void>;
  /** After the user's profile changes (e.g. their name). */
  setUser: (user: MeResponse) => void;
  dispose: () => void;
}

interface SessionStoreOptions {
  queryClient: QueryClient;
  /** Deletes the saved (persisted) copy of the API cache. */
  clearSavedData?: () => Promise<void>;
}

/** Not a secret: who was last signed in here, so the app can start offline. */
const lastUserKey = "wwg:last-user";
/** Set when signing out couldn't reach the API, so the refresh cookie may still be valid. */
const signOutPendingKey = "wwg:sign-out-pending";

export function createSessionStore({
  queryClient,
  clearSavedData,
}: SessionStoreOptions): SessionStore {
  let state: SessionState = { status: "starting", user: null, ended: false };
  const listeners = new Set<() => void>();
  let resolveReady: () => void = () => undefined;
  const ready = new Promise<void>((resolve) => {
    resolveReady = resolve;
  });
  const channel =
    typeof BroadcastChannel === "undefined" ? null : new BroadcastChannel("wwg:session");

  function set(next: SessionState) {
    state = next;
    for (const listener of listeners) {
      listener();
    }
  }

  async function forgetData() {
    queryClient.clear();
    try {
      await clearSavedData?.();
    } catch {
      // IndexedDB unavailable: the in-memory cache is gone, and it's saved again from that.
    }
  }

  async function forgetSession(ended: boolean) {
    clearAccessToken();
    await forgetData();
    storage.remove(lastUserKey);
    set({ status: "signed-out", user: null, ended });
  }

  async function becomeSignedIn(user: MeResponse) {
    if (readLastUser()?.id !== user.id) {
      // Saved data is only kept for the same person signing back in: never show anyone else the
      // previous user's data. (That includes data restored from IndexedDB just after signing out
      // cleared it.)
      await forgetData();
    }
    storage.write(lastUserKey, user);
    set({ status: "signed-in", user, ended: false });
  }

  function goOffline() {
    const last = readLastUser();
    set(
      last
        ? { status: "offline", user: last, ended: false }
        : { status: "signed-out", user: null, ended: false },
    );
  }

  async function start() {
    try {
      if (storage.read(signOutPendingKey) === true) {
        // Finish signing out before anything else; stay signed out either way.
        await tryLogout();
        await forgetSession(false);
        return;
      }

      const outcome = await refreshAccessToken();
      if (outcome === "refreshed") {
        await loadUser();
      } else if (outcome === "signed-out") {
        await forgetSession(false);
      } else {
        goOffline();
      }
    } catch {
      // Nothing above should throw, but a start-up that never settles is a blank app.
      goOffline();
    } finally {
      resolveReady();
    }
  }

  /** Fetches the signed-in user (after a refresh); offline if that fails. */
  async function loadUser() {
    const user = await getMe().catch(() => null);
    if (user) {
      await becomeSignedIn(user);
    } else if (state.status !== "signed-in") {
      goOffline();
    }
  }

  async function tryLogout() {
    try {
      await logout();
      storage.remove(signOutPendingKey);
    } catch {
      storage.write(signOutPendingKey, true);
    }
  }

  async function signIn(token: TokenResponse) {
    storage.remove(signOutPendingKey);
    setAccessToken(token);
    let user: MeResponse;
    try {
      user = await getMe();
    } catch (error) {
      // Stay cleanly signed out rather than holding a token with no user.
      clearAccessToken();
      throw error;
    }
    await becomeSignedIn(user);
    channel?.postMessage("signed-in");
  }

  async function switchUser(token: TokenResponse) {
    await forgetData();
    storage.remove(lastUserKey);
    await signIn(token);
    channel?.postMessage("switched");
  }

  async function signOut() {
    await tryLogout();
    await forgetSession(false);
    channel?.postMessage("signed-out");
  }

  const stopSignedOut = onSignedOut(() => {
    if (state.status !== "signed-out") {
      void forgetSession(true);
    }
  });
  // Keeps the user (name, isAdmin) current, and brings an offline start back online once the API
  // answers. At start-up and sign-in the user is fetched there instead.
  const stopRefreshed = onRefreshed(() => {
    if (state.status === "signed-in" || state.status === "offline") {
      void loadUser();
    }
  });
  const stopOnline = onlineManager.subscribe((online) => {
    if (online && state.status === "offline") {
      void start();
    }
  });
  channel?.addEventListener("message", (event: MessageEvent<unknown>) => {
    if (event.data === "signed-out") {
      void forgetSession(false);
    } else if (event.data === "switched") {
      // Another tab started or ended a masquerade: this one's data is someone else's now.
      void forgetData().then(start);
    } else if (event.data === "signed-in" && state.status !== "signed-in") {
      // Another tab signed in: get this tab its own access token from the shared cookie.
      void start();
    }
  });

  return {
    getState: () => state,
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    ready,
    start,
    signIn,
    switchUser,
    signOut,
    setUser(user) {
      storage.write(lastUserKey, user);
      set({ ...state, user });
    },
    dispose() {
      stopSignedOut();
      stopRefreshed();
      stopOnline();
      channel?.close();
    },
  };
}

/** The last user record, if there is one and it still has the right shape. */
function readLastUser(): MeResponse | null {
  const stored = storage.read(lastUserKey);
  // Saved before masquerades existed: not one.
  const parsed = GetMeResponse.safeParse(
    typeof stored === "object" && stored !== null ? { masquerade: null, ...stored } : stored,
  );
  return parsed.success ? parsed.data : null;
}

const storage = {
  read(key: string): unknown {
    try {
      const value = localStorage.getItem(key);
      return value === null ? null : (JSON.parse(value) as unknown);
    } catch {
      return null;
    }
  },
  write(key: string, value: unknown) {
    try {
      localStorage.setItem(key, JSON.stringify(value));
    } catch {
      // Storage unavailable (e.g. private mode): the app just can't start offline.
    }
  },
  remove(key: string) {
    try {
      localStorage.removeItem(key);
    } catch {
      // As above.
    }
  },
};
