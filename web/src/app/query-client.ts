import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/api-fetch";

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Refetched on window focus and reconnect (the defaults) once older than this.
        staleTime: 30_000,
        // A 4xx won't succeed on a retry; network errors and 5xx get up to two more tries.
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.status < 500) && failureCount < 2,
      },
    },
  });
}
