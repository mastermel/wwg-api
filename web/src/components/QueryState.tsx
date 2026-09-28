import { Alert, Loader, Text } from "@mantine/core";
import { IconAlertTriangle, IconCloudOff } from "@tabler/icons-react";
import type { UseQueryResult } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { ApiError } from "@/lib/api-fetch";

interface QueryStateProps<TData> {
  query: UseQueryResult<TData>;
  /** What the data looks like once it's there (fresh, or saved from before going offline). */
  children: (data: TData) => ReactNode;
}

/**
 * The standard loading / error / offline handling for a page's data. Offline, a query with no
 * saved data is paused rather than failing; that shows "not available offline", not a spinner.
 */
export function QueryState<TData>({ query, children }: QueryStateProps<TData>) {
  // Data wins, even if a later refetch failed (offline, or the server is down): showing what we
  // have beats an error.
  if (query.data !== undefined) {
    return children(query.data);
  }

  if (query.isPending && query.fetchStatus === "paused") {
    return (
      <Alert color="gray" icon={<IconCloudOff aria-hidden />} title="Not available offline">
        This hasn't been saved on this device yet. It will load when you're back online.
      </Alert>
    );
  }

  if (query.error instanceof ApiError && [403, 404].includes(query.error.status)) {
    return (
      <Alert color="gray" icon={<IconAlertTriangle aria-hidden />} title="Not found">
        This doesn&apos;t exist, or you don&apos;t have access to it.
      </Alert>
    );
  }

  if (query.isError) {
    return (
      <Alert color="red" icon={<IconAlertTriangle aria-hidden />} title="This didn't load">
        <Text size="sm">{query.error.message}</Text>
      </Alert>
    );
  }

  return <Loader size="sm" role="status" aria-label="Loading" />;
}
