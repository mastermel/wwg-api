import "@testing-library/jest-dom/vitest";
import { notifications } from "@mantine/notifications";
import { onlineManager } from "@tanstack/react-query";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll } from "vitest";
import { resetAccessToken } from "@/lib/access-token";
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

// jsdom has no document.fonts; Mantine's auto-sizing Textarea listens on it for font loads.
Object.defineProperty(document, "fonts", {
  value: { addEventListener: () => undefined, removeEventListener: () => undefined },
  configurable: true,
});

// jsdom doesn't implement these; the router's scroll restoration and axe call them.
window.scrollTo = () => undefined;
// Mantine's Select scrolls the highlighted option into view.
Element.prototype.scrollIntoView = () => undefined;
HTMLCanvasElement.prototype.getContext = () => null;

// Any request without a handler fails the test, so tests never reach a real network.
beforeAll(() => {
  server.listen({ onUnhandledRequest: "error" });
});

afterEach(() => {
  // Mantine's notifications live in one store for the whole run: without this, earlier tests'
  // fill the three on screen and a test's own wait unseen in the queue.
  notifications.clean();
  // Vitest globals are off, so Testing Library can't register its own cleanup.
  cleanup();
  // Back online only after unmounting: done while a page is still mounted, it resumes paused
  // queries, whose requests then find no handler.
  onlineManager.setOnline(true);
  resetAccessToken();
  server.resetHandlers();
});

afterAll(() => {
  server.close();
});
