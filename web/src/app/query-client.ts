import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/api-fetch";

/** How long cached API data is kept (and persisted for offline use): the session's length. */
export const cacheMaxAge = 30 * 24 * 60 * 60 * 1000;

declare module "@tanstack/react-query" {
  interface Register {
    queryMeta: {
      /** false: never save this query for offline use (live status, admin data). */
      persist?: boolean;
    };
  }
}

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Refetched on window focus and reconnect (the defaults) once older than this.
        staleTime: 30_000,
        // Kept at least as long as the persisted copy, or it would be dropped before saving.
        gcTime: cacheMaxAge,
        // A 4xx won't succeed on a retry; network errors and 5xx get up to two more tries.
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.status < 500) && failureCount < 2,
      },
    },
  });
}
