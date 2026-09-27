import { onlineManager } from "@tanstack/react-query";
import { useSyncExternalStore } from "react";

/** Whether the browser is online, as TanStack Query sees it (it pauses fetches while offline). */
export function useOnline() {
  return useSyncExternalStore(
    (onChange) => onlineManager.subscribe(onChange),
    () => onlineManager.isOnline(),
  );
}
