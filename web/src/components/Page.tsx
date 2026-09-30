import { Box, Divider, Group, Stack, Title } from "@mantine/core";
import { useContext, useEffect, useRef, type ReactNode } from "react";
import { CompactPageContext } from "@/components/page-context";

const appName = "Wasatch Wargamers";

// The first page load keeps the browser's normal focus; later navigations move it.
let hasNavigated = false;

interface PageProps {
  title: string;
  /** Above the title: usually a link back to where this page belongs (e.g. its campaign). */
  back?: ReactNode;
  /** Under the title: a line or two of context (a role badge, the Umpire, counts). */
  summary?: ReactNode;
  /** The page's own actions, beside the title (below it on phones). */
  actions?: ReactNode;
  children?: ReactNode;
}

/**
 * Every page renders inside this. It sets the document title and, after client-side navigation,
 * moves focus to the page's single h1, so screen readers announce the new page (WCAG 2.4.2, 2.4.3).
 * The header (back link, title, summary, actions) is divided from the page's sections.
 */
export function Page({ title, back, summary, actions, children }: PageProps) {
  const heading = useRef<HTMLHeadingElement>(null);
  const compact = useContext(CompactPageContext);

  useEffect(() => {
    document.title = `${title} · ${appName}`;
    if (hasNavigated) {
      heading.current?.focus();
    }
    hasNavigated = true;
  }, [title]);

  const titleElement = (
    <Title
      order={1}
      ref={heading}
      tabIndex={-1}
      style={{ outline: "none" }}
      size={compact ? "h2" : undefined}
    >
      {title}
    </Title>
  );

  if (compact) {
    return (
      <Stack gap="lg">
        {titleElement}
        {children}
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Stack gap="xs" component="header">
        {back}
        <Group justify="space-between" align="flex-end" gap="md">
          <Stack gap={6} miw={0}>
            {titleElement}
            {summary && (
              <Box fz="sm" c="dimmed">
                {summary}
              </Box>
            )}
          </Stack>
          {actions && <Group gap="sm">{actions}</Group>}
        </Group>
        <Divider mt="sm" />
      </Stack>
      {children}
    </Stack>
  );
}
