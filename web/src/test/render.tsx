import { createMemoryHistory, RouterProvider } from "@tanstack/react-router";
import { render } from "@testing-library/react";
import axe from "axe-core";
import { expect } from "vitest";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { createAppRouter } from "@/app/router";

/**
 * Renders the whole app (real routes, theme and providers) at `path`, with a fresh API cache.
 * API calls go to the MSW test server; add handlers with `server.use(...)` first.
 */
export async function renderApp(path: string) {
  const queryClient = createQueryClient();
  queryClient.setDefaultOptions({
    queries: { ...queryClient.getDefaultOptions().queries, retry: false },
  });
  const router = createAppRouter(createMemoryHistory({ initialEntries: [path] }));

  const result = render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  await router.load();
  return { ...result, router };
}

/** Fails the test with a readable list if axe finds accessibility violations. */
export async function expectNoAxeViolations(container: Element) {
  const results = await axe.run(container);
  expect(results.violations.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
}
