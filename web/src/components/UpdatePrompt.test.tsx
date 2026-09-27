import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { UpdatePrompt } from "@/components/UpdatePrompt";

const pwa = vi.hoisted(() => ({
  needRefresh: false,
  setNeedRefresh: vi.fn(),
  updateServiceWorker: vi.fn(() => Promise.resolve()),
}));

vi.mock("virtual:pwa-register/react", () => ({
  useRegisterSW: () => ({
    needRefresh: [pwa.needRefresh, pwa.setNeedRefresh],
    offlineReady: [false, vi.fn()],
    updateServiceWorker: pwa.updateServiceWorker,
  }),
}));

function setController(controller: object | null) {
  Object.defineProperty(navigator, "serviceWorker", { value: { controller }, configurable: true });
}

function renderPrompt() {
  render(
    <AppProviders queryClient={createQueryClient()}>
      <UpdatePrompt />
    </AppProviders>,
  );
}

describe("UpdatePrompt", () => {
  beforeEach(() => {
    pwa.needRefresh = true;
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("shows nothing until a new version is ready", () => {
    pwa.needRefresh = false;

    renderPrompt();

    expect(screen.queryByText("Update available")).not.toBeInTheDocument();
  });

  it("activates the waiting version when a service worker controls the page", async () => {
    setController({});
    renderPrompt();

    await userEvent.click(screen.getByRole("button", { name: "Reload" }));

    expect(pwa.updateServiceWorker).toHaveBeenCalledWith(true);
  });

  it("just reloads on a first visit, when no service worker controls the page", async () => {
    setController(null);
    const reload = vi.fn();
    vi.stubGlobal("location", { reload });
    renderPrompt();

    await userEvent.click(screen.getByRole("button", { name: "Reload" }));

    expect(reload).toHaveBeenCalled();
    expect(pwa.updateServiceWorker).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("can be dismissed for now", async () => {
    renderPrompt();

    await userEvent.click(screen.getByRole("button", { name: "Not now" }));

    expect(pwa.setNeedRefresh).toHaveBeenCalledWith(false);
  });
});
