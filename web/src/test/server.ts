import { http, HttpResponse } from "msw";
import { setupServer } from "msw/node";
import type { HealthStatus } from "@/api/generated/model";

/**
 * Answers for pages many tests pass through (e.g. the campaign list, where sign-in lands).
 * A test's own `server.use(...)` handlers take precedence over these.
 */
const defaults = [
  http.get("*/api/campaigns", () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0 }),
  ),
  // Every campaign page shows its armies and members, and its join link to the Umpire.
  http.get("*/api/campaigns/:id/members", () => HttpResponse.json([])),
  http.get("*/api/campaigns/:id/armies", () => HttpResponse.json([])),
  http.get("*/api/campaigns/:id/sides", () => HttpResponse.json([])),
  // The map page: a campaign setting up, with nothing on the map.
  http.get("*/api/campaigns/:id/units", () => HttpResponse.json([])),
  http.get("*/api/factions", () => HttpResponse.json([])),
  http.get("*/api/campaigns/:id/positions", () => HttpResponse.json([])),
  http.get("*/api/campaigns/:id/grid", () => HttpResponse.json({ cells: [], edges: [] })),
  http.get("*/api/campaigns/:id/turns", () =>
    HttpResponse.json({ stage: "Setup", openTurn: 0, turns: [], startProblems: [] }),
  ),
  http.get("*/api/campaigns/:id/join-code", () =>
    HttpResponse.json({ joinCode: "test-join-code" }),
  ),
];

/**
 * The mock API for tests. Tests add their own handlers with `server.use(...)` (msw's `http.*`),
 * which are reset after each test, back to `defaults`.
 */
export const server = setupServer(...defaults);

/** `GET /health` answering with `status`. */
export const serveHealth = (status: HealthStatus = "Healthy") =>
  http.get("*/health", () => HttpResponse.json({ status }));
