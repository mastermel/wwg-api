import { Alert, Button, Group } from "@mantine/core";
import { IconAlertTriangle, IconRefresh } from "@tabler/icons-react";
import type { ErrorComponentProps } from "@tanstack/react-router";
import { Page } from "@/components/Page";

/** Shown when a page fails to render or load (the router's error boundary). */
export function ErrorPage({ error, reset }: ErrorComponentProps) {
  return (
    <Page title="Something went wrong">
      <Alert color="red" icon={<IconAlertTriangle aria-hidden />} title="This page didn't load">
        {error instanceof Error ? error.message : "An unexpected error occurred."}
      </Alert>
      <Group>
        <Button leftSection={<IconRefresh size={18} aria-hidden />} onClick={reset}>
          Try again
        </Button>
      </Group>
    </Page>
  );
}
