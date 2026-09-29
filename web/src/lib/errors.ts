import { ApiError } from "@/lib/api-fetch";

/**
 * What to tell the user about a failed API call. Rate limits and server errors get their own
 * message; otherwise the API's `detail` (e.g. "Mel already commands First Corps."), else
 * `fallback`, which says what didn't happen.
 */
export function errorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof ApiError)) {
    return "Couldn't reach the server. Check your connection and try again.";
  }
  if (error.status === 429) {
    return "Too many requests. Wait a minute, then try again.";
  }
  if (error.status >= 500) {
    return "Something went wrong on the server. Try again in a moment.";
  }
  if (error.problem?.detail) {
    return error.problem.detail;
  }
  // Not from the auth endpoints (they explain themselves): refreshing the session failed for a
  // passing reason, such as a rate limit.
  return error.status === 401
    ? "Your session couldn't be checked. Try again, or reload the page."
    : fallback;
}

/** Errors that say nothing about the request itself, only that the server can't take it now. */
export function isServerTrouble(error: unknown) {
  return error instanceof ApiError && (error.status === 429 || error.status >= 500);
}
