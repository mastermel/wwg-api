import { Paper, Stack, Text, Title } from "@mantine/core";
import type { ReactNode } from "react";

export function AccountSection({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <Paper withBorder p="lg" radius="md" component="section" aria-label={title}>
      <Stack gap="md">
        <div>
          <Title order={2} size="h4">
            {title}
          </Title>
          {description && (
            <Text size="sm" c="dimmed">
              {description}
            </Text>
          )}
        </div>
        {children}
      </Stack>
    </Paper>
  );
}
