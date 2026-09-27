import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "@/test/server";

// Any request without a handler fails the test, so tests never reach a real network.
beforeAll(() => {
  server.listen({ onUnhandledRequest: "error" });
});

afterEach(() => {
  // Vitest globals are off, so Testing Library can't register its own cleanup.
  cleanup();
  server.resetHandlers();
});

afterAll(() => {
  server.close();
});
