import { Button, type ButtonProps } from "@mantine/core";
import type { ReactNode } from "react";

interface LinkButtonProps extends ButtonProps {
  /** Draws the router Link the button is. */
  renderLink: (props: Record<string, unknown>) => ReactNode;
  children: ReactNode;
}

/**
 * A button that goes to another page. While disabled (e.g. offline) it's a real disabled button:
 * a disabled-looking link can still be followed with Enter.
 */
export function LinkButton({ renderLink, disabled, ...props }: LinkButtonProps) {
  return disabled ? <Button disabled {...props} /> : <Button {...props} renderRoot={renderLink} />;
}
