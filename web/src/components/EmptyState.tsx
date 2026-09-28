import { Stack, Text, ThemeIcon } from "@mantine/core";
import type { Icon } from "@tabler/icons-react";
import type { ReactNode } from "react";

interface EmptyStateProps {
  icon: Icon;
  title: string;
  /** What to do about it. */
  children?: ReactNode;
  /** A button that does it. */
  action?: ReactNode;
}

/** Nothing here yet: what's missing and what to do next (DESIGN.md §3.12, empty states). */
export function EmptyState({ icon: IconComponent, title, children, action }: EmptyStateProps) {
  return (
    <Stack align="center" gap="xs" py="lg" px="md" ta="center">
      <ThemeIcon variant="light" size={48} radius="xl" aria-hidden>
        <IconComponent size={26} />
      </ThemeIcon>
      <Text fw={600}>{title}</Text>
      {children && (
        <Text size="sm" c="dimmed" maw={420}>
          {children}
        </Text>
      )}
      {action}
    </Stack>
  );
}
