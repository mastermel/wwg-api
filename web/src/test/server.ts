import { http, HttpResponse } from "msw";
import { setupServer } from "msw/node";

/**
 * Answers for pages many tests pass through (e.g. the campaign list, where sign-in lands).
 * A test's own `server.use(...)` handlers take precedence over these.
 */
const defaults = [
  http.get("*/api/campaigns", () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0 }),
  ),
];

/**
 * The mock API for tests. Tests add handlers with `server.use(...)`, usually the Orval-generated
 * ones (e.g. `getGetHealthMockHandler`); they're reset after each test, back to `defaults`.
 */
export const server = setupServer(...defaults);
