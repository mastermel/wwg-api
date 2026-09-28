import { Anchor, Group } from "@mantine/core";
import { IconArrowLeft } from "@tabler/icons-react";
import type { ReactNode } from "react";

/** A page's way back up, above its title (Page's `back`). `renderLink` draws the router Link. */
export function BackLink({
  children,
  renderLink,
}: {
  children: ReactNode;
  renderLink: (props: Record<string, unknown>) => ReactNode;
}) {
  return (
    <Anchor size="sm" fw={500} renderRoot={renderLink} w="fit-content">
      <Group gap={4} wrap="nowrap">
        <IconArrowLeft size={16} aria-hidden /> {children}
      </Group>
    </Anchor>
  );
}
