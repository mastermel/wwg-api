import "@mantine/core/styles.css";
import "@mantine/notifications/styles.css";

import { MantineProvider } from "@mantine/core";
import { Notifications } from "@mantine/notifications";
import { QueryClientProvider, type QueryClient } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { cssVariablesResolver, theme } from "@/app/theme";

interface AppProvidersProps {
  queryClient: QueryClient;
  children: ReactNode;
}

/** Everything the app (and component tests) render inside: theme, notifications, API cache. */
export function AppProviders({ queryClient, children }: AppProvidersProps) {
  return (
    // Light or dark follows the operating system; there's no in-app toggle.
    <MantineProvider
      theme={theme}
      cssVariablesResolver={cssVariablesResolver}
      defaultColorScheme="auto"
    >
      <Notifications />
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </MantineProvider>
  );
}
