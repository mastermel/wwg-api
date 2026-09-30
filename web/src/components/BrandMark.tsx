import { Box, type BoxProps } from "@mantine/core";

interface BrandMarkProps extends BoxProps {
  /** Height, in px; the width follows the figure's shape. */
  size: number;
}

/** logo.svg's width over its height (its viewBox). */
const aspect = 0.398;

/**
 * The app's mark (an officer in the uniform of the late 18th century), drawn in the current text
 * colour so it follows light and dark mode. The SVG is a CSS mask, so there's one artwork file for
 * every use; the details on the figure cut through to what's behind.
 */
export function BrandMark({ size, ...props }: BrandMarkProps) {
  const mask = "url(/logo.svg) center / contain no-repeat";
  return (
    <Box
      aria-hidden
      w={Math.round(size * aspect)}
      h={size}
      bg="currentColor"
      style={{ mask, WebkitMask: mask, flexShrink: 0 }}
      {...props}
    />
  );
}
