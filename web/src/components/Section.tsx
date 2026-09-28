import { Box, Divider, Group, Paper, Stack, Text, Title } from "@mantine/core";
import { useId, type ReactNode } from "react";
import classes from "@/components/Section.module.css";

interface SectionProps {
  title: string;
  description?: ReactNode;
  /** Buttons for the section, beside its title. */
  actions?: ReactNode;
  /** Content that runs to the panel's edges (a table), rather than padded. */
  flush?: boolean;
  /** "danger": a red-edged panel for actions that delete or remove, apart from the rest. */
  tone?: "danger";
  children: ReactNode;
}

/**
 * A titled panel: one part of a page (Armies, Members, Your name). A region named by its title,
 * so screen reader users can jump between them; the title is an h2 under the page's h1.
 */
export function Section({ title, description, actions, flush, tone, children }: SectionProps) {
  const id = useId();
  return (
    <Paper
      withBorder
      component="section"
      aria-labelledby={id}
      className={classes.section}
      data-tone={tone}
    >
      <Group justify="space-between" align="center" gap="sm" px="lg" py="md" wrap="wrap">
        <Stack gap={2} miw={0}>
          <Title order={2} size="h4" id={id} className={classes.title}>
            {title}
          </Title>
          {description && (
            <Text size="sm" c="dimmed">
              {description}
            </Text>
          )}
        </Stack>
        {actions && <Group gap="xs">{actions}</Group>}
      </Group>
      <Divider />
      <Box p={flush ? 0 : "lg"}>{children}</Box>
    </Paper>
  );
}
