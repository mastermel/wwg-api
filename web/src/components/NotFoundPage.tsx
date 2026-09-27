import { Button, Text } from "@mantine/core";
import { IconMapSearch } from "@tabler/icons-react";
import { Link } from "@tanstack/react-router";
import { Page } from "@/components/Page";

export function NotFoundPage() {
  return (
    <Page title="Page not found">
      <Text>
        <IconMapSearch size={20} aria-hidden style={{ verticalAlign: "text-bottom" }} /> There's
        nothing at this address. It may have moved, or the link may be wrong.
      </Text>
      <div>
        <Button component={Link} to="/">
          Go to the start page
        </Button>
      </div>
    </Page>
  );
}
