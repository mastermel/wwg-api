import { Stack, Title } from "@mantine/core";
import { useEffect, useRef, type ReactNode } from "react";

const appName = "WWG Campaigner";

// The first page load keeps the browser's normal focus; later navigations move it.
let hasNavigated = false;

interface PageProps {
  title: string;
  children?: ReactNode;
}

/**
 * Every page renders inside this. It sets the document title and, after client-side navigation,
 * moves focus to the page's single h1, so screen readers announce the new page (WCAG 2.4.2, 2.4.3).
 */
export function Page({ title, children }: PageProps) {
  const heading = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    document.title = `${title} · ${appName}`;
    if (hasNavigated) {
      heading.current?.focus();
    }
    hasNavigated = true;
  }, [title]);

  return (
    <Stack gap="lg">
      <Title order={1} ref={heading} tabIndex={-1} style={{ outline: "none" }}>
        {title}
      </Title>
      {children}
    </Stack>
  );
}
