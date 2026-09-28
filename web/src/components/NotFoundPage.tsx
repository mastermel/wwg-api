import { Button, Paper } from "@mantine/core";
import { IconMapSearch } from "@tabler/icons-react";
import { Link } from "@tanstack/react-router";
import { EmptyState } from "@/components/EmptyState";
import { Page } from "@/components/Page";

export function NotFoundPage() {
  return (
    <Page title="Page not found">
      <Paper withBorder>
        <EmptyState
          icon={IconMapSearch}
          title="There's nothing at this address."
          action={
            <Button component={Link} to="/" mt="sm">
              Go to the start page
            </Button>
          }
        >
          It may have moved, or the link may be wrong.
        </EmptyState>
      </Paper>
    </Page>
  );
}
