import { useState } from "react";

/**
 * What a confirmation modal is about (the unit being deleted, the Player being removed). Closing
 * keeps the target, so the modal's text doesn't go blank during its closing animation.
 */
export function useConfirmTarget<T>() {
  const [target, setTarget] = useState<T | null>(null);
  const [opened, setOpened] = useState(false);
  return {
    target,
    opened,
    open: (next: T) => {
      setTarget(next);
      setOpened(true);
    },
    close: () => {
      setOpened(false);
    },
  };
}
