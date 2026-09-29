import { Alert } from "@mantine/core";
import { IconCloudOff } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useOnline } from "@/lib/use-online";

const timeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

/** Shown while offline: the app is read-only, showing data from when it was last online. */
export function OfflineBanner() {
  const online = useOnline();
  const queryClient = useQueryClient();

  if (online) {
    return null;
  }

  const lastUpdated = Math.max(
    0,
    ...queryClient
      .getQueryCache()
      .getAll()
      .filter((query) => query.meta?.persist !== false)
      .map((query) => query.state.dataUpdatedAt),
  );

  return (
    <Alert
      role="status"
      color="yellow"
      icon={<IconCloudOff aria-hidden />}
      title="You're offline"
      mb="lg"
    >
      {lastUpdated > 0
        ? `Showing saved data from ${timeFormat.format(lastUpdated)}. Changes are paused until you're back online.`
        : "Changes are paused until you're back online."}
    </Alert>
  );
}
