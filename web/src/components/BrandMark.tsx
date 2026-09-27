import { Box, type BoxProps } from "@mantine/core";

interface BrandMarkProps extends BoxProps {
  /** Width and height, in px. */
  size: number;
}

/**
 * The app's mark (Napoleon on a rearing horse), drawn in the current text colour so it follows
 * light and dark mode. The SVG is a CSS mask, so there's one artwork file for every use.
 */
export function BrandMark({ size, ...props }: BrandMarkProps) {
  const mask = "url(/logo.svg) center / contain no-repeat";
  return (
    <Box
      aria-hidden
      w={size}
      h={size}
      bg="currentColor"
      style={{ mask, WebkitMask: mask, flexShrink: 0 }}
      {...props}
    />
  );
}
