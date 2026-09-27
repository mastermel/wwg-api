import { createMemoryHistory, RouterProvider } from "@tanstack/react-router";
import { render } from "@testing-library/react";
import axe from "axe-core";
import { afterEach, expect } from "vitest";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { createAppRouter } from "@/app/router";
import { SessionProvider } from "@/features/auth/SessionProvider";
import { createSessionStore, type SessionStore } from "@/features/auth/session-store";
import { mockSession } from "@/test/session";

const stores: SessionStore[] = [];
afterEach(() => {
  for (const store of stores.splice(0)) store.dispose();
  localStorage.clear();
});

interface RenderAppOptions {
  /** The session the mock API has. Default: signed in as testUser. */
  session?: "signed-in" | "signed-out" | "offline";
}

/**
 * Renders the whole app (real routes, theme and providers) at `path`, with a fresh API cache and
 * session. API calls go to the MSW test server; add handlers with `server.use(...)` first (the
 * session endpoints are mocked here).
 */
export async function renderApp(path: string, { session = "signed-in" }: RenderAppOptions = {}) {
  const calls = mockSession(session);
  const queryClient = createQueryClient();
  queryClient.setDefaultOptions({
    queries: { ...queryClient.getDefaultOptions().queries, retry: false },
  });
  const store = createSessionStore({ queryClient });
  stores.push(store);
  await store.start();
  const router = createAppRouter(store, createMemoryHistory({ initialEntries: [path] }));

  const result = render(
    <AppProviders queryClient={queryClient}>
      <SessionProvider store={store}>
        <RouterProvider router={router} />
      </SessionProvider>
    </AppProviders>,
  );
  await router.load();
  return { ...result, router, session: store, calls };
}

/** Fails the test with a readable list if axe finds accessibility violations. */
export async function expectNoAxeViolations(container: Element) {
  const results = await axe.run(container);
  expect(results.violations.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
}
