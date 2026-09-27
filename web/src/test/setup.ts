import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "@/test/server";

// Browser APIs Mantine uses that jsdom doesn't have.
window.matchMedia = (query: string) =>
  ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  }) satisfies MediaQueryList;

window.ResizeObserver = class {
  observe() {
    // Layout never changes in jsdom.
  }
  unobserve() {
    // Nothing observed.
  }
  disconnect() {
    // Nothing observed.
  }
};

// jsdom doesn't implement these; the router's scroll restoration and axe call them.
window.scrollTo = () => undefined;
HTMLCanvasElement.prototype.getContext = () => null;

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
