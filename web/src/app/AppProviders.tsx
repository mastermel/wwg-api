import "@mantine/core/styles.css";
import "@mantine/notifications/styles.css";
import "@/app/app.css";

import { MantineProvider } from "@mantine/core";
import { Notifications } from "@mantine/notifications";
import { QueryClientProvider, type QueryClient } from "@tanstack/react-query";
import {
  PersistQueryClientProvider,
  type PersistQueryClientProviderProps,
} from "@tanstack/react-query-persist-client";
import type { ReactNode } from "react";
import { cssVariablesResolver, theme } from "@/app/theme";

interface AppProvidersProps {
  queryClient: QueryClient;
  /** Save the API cache for offline use. The app passes it; tests don't. */
  persistOptions?: PersistQueryClientProviderProps["persistOptions"];
  children: ReactNode;
}

/** Everything the app (and component tests) render inside: theme, notifications, API cache. */
export function AppProviders({ queryClient, persistOptions, children }: AppProvidersProps) {
  return (
    // Light or dark follows the operating system; there's no in-app toggle.
    <MantineProvider
      theme={theme}
      cssVariablesResolver={cssVariablesResolver}
      defaultColorScheme="auto"
    >
      {/* At the top: at the bottom they'd cover the phone tab bar. */}
      <Notifications position="top-right" limit={3} />
      {persistOptions ? (
        <PersistQueryClientProvider
          client={queryClient}
          persistOptions={persistOptions}
          // Restored data is a saved copy, however recent: refetch what's on screen, as a reload
          // should. (Offline, the refetch waits and the saved copy stays.)
          onSuccess={() => void queryClient.invalidateQueries()}
        >
          {children}
        </PersistQueryClientProvider>
      ) : (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      )}
    </MantineProvider>
  );
}
