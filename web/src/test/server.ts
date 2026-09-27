import { setupServer } from "msw/node";

/**
 * The mock API for tests. Tests add handlers with `server.use(...)`, usually the Orval-generated
 * ones (e.g. `getGetHealthMockHandler`); they're reset after each test.
 */
export const server = setupServer();
