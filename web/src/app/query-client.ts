import { notifications } from "@mantine/notifications";
import { QueryCache, QueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/api-fetch";
import { errorMessage, isServerTrouble } from "@/lib/errors";

/** How long cached API data is kept (and persisted for offline use): the session's length. */
export const cacheMaxAge = 30 * 24 * 60 * 60 * 1000;

/** The longest delay setTimeout can hold (a signed 32-bit number of milliseconds). */
const maxTimerDelay = 2 ** 31 - 1;

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
    queryCache: new QueryCache({
      // Pages show their own "didn't load" for a query with nothing to show. A rate limit or a
      // server error also gets a notification, since it's also why data on screen didn't
      // update. One of each at a time (the id), however many queries fail together.
      onError: (error) => {
        if (isServerTrouble(error)) {
          notifications.show({
            id: error instanceof ApiError && error.status === 429 ? "rate-limited" : "server-error",
            color: "red",
            message: errorMessage(error, ""),
          });
        }
      },
    }),
    defaultOptions: {
      queries: {
        // Refetched on window focus and reconnect (the defaults) once older than this.
        staleTime: 30_000,
        // Kept as long as possible, so unused data is still there to save for offline use. Not
        // cacheMaxAge: timers overflow past 2^31 - 1 ms (about 24.8 days) and fire at once, which
        // dropped every restored query before anything could use it.
        gcTime: maxTimerDelay,
        // A 4xx won't succeed on a retry; network errors and 5xx get up to two more tries.
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.status < 500) && failureCount < 2,
      },
    },
  });
}
