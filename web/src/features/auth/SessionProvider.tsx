import type { ReactNode } from "react";
import { SessionContext } from "@/features/auth/session-context";
import type { SessionStore } from "@/features/auth/session-store";

export function SessionProvider({ store, children }: { store: SessionStore; children: ReactNode }) {
  return <SessionContext value={store}>{children}</SessionContext>;
}
