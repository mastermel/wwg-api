import { createContext, use, useSyncExternalStore } from "react";
import type { SessionState, SessionStore } from "@/features/auth/session-store";

export const SessionContext = createContext<SessionStore | null>(null);

export function useSessionStore(): SessionStore {
  const store = use(SessionContext);
  if (!store) {
    throw new Error("useSessionStore must be used inside SessionProvider");
  }
  return store;
}

/** The current session, re-rendering when it changes. */
export function useSession(): SessionState {
  const store = useSessionStore();
  return useSyncExternalStore(store.subscribe, store.getState);
}
