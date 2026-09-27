import { createRouter, type RouterHistory } from "@tanstack/react-router";
import type { SessionStore } from "@/features/auth/session-store";
import { routeTree } from "@/routeTree.gen";

/** What every route's beforeLoad can use. */
export interface RouterContext {
  session: SessionStore;
}

export function createAppRouter(session: SessionStore, history?: RouterHistory) {
  return createRouter({
    routeTree,
    context: { session },
    defaultPreload: "intent",
    scrollRestoration: true,
    ...(history ? { history } : {}),
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}
