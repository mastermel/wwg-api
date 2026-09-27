import type { TokenResponse } from "@/api/generated/model";

/**
 * The access token, in memory only (the refresh token is an HttpOnly cookie script can't read;
 * decision 0004). Refreshed shortly before it expires, and on demand after a 401.
 */

export type RefreshOutcome = "refreshed" | "signed-out" | "unavailable";

let accessToken: string | null = null;
let refreshTimer: ReturnType<typeof setTimeout> | undefined;
let inFlight: Promise<RefreshOutcome> | null = null;
const signedOutListeners = new Set<() => void>();

export function getAccessToken() {
  return accessToken;
}

export function setAccessToken(token: TokenResponse) {
  accessToken = token.accessToken;
  // A minute early (or halfway, for very short lifetimes), so calls never race the expiry.
  scheduleRefresh(Math.max(token.expiresIn - 60, token.expiresIn / 2));
}

export function clearAccessToken() {
  accessToken = null;
  clearTimeout(refreshTimer);
}

/** Called when a refresh finds the session has ended (401), e.g. a password change elsewhere. */
export function onSignedOut(listener: () => void) {
  signedOutListeners.add(listener);
  return () => {
    signedOutListeners.delete(listener);
  };
}

/**
 * Swaps the refresh cookie for a new access token. Concurrent callers share one request. Only a
 * 401 means signed out; anything else (offline, 429, 5xx) is "unavailable": try again later.
 */
export function refreshAccessToken(): Promise<RefreshOutcome> {
  inFlight ??= requestRefresh().finally(() => {
    inFlight = null;
  });
  return inFlight;
}

async function requestRefresh(): Promise<RefreshOutcome> {
  let response: Response;
  try {
    response = await fetch(new URL("/api/auth/refresh", window.location.origin), {
      method: "POST",
    });
  } catch {
    scheduleRefresh(60);
    return "unavailable";
  }

  if (response.ok) {
    setAccessToken((await response.json()) as TokenResponse);
    return "refreshed";
  }

  if (response.status === 401) {
    clearAccessToken();
    for (const listener of signedOutListeners) {
      listener();
    }
    return "signed-out";
  }

  scheduleRefresh(60);
  return "unavailable";
}

function scheduleRefresh(inSeconds: number) {
  clearTimeout(refreshTimer);
  refreshTimer = setTimeout(() => void refreshAccessToken(), inSeconds * 1000);
}

/** For tests: forget everything between tests. */
export function resetAccessToken() {
  clearAccessToken();
  inFlight = null;
  signedOutListeners.clear();
}
