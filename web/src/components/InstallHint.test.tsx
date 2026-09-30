import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { InstallHint } from "@/components/InstallHint";

function renderHint() {
  render(
    <AppProviders queryClient={createQueryClient()}>
      <InstallHint />
    </AppProviders>,
  );
}

function setUserAgent(ua: string) {
  vi.spyOn(navigator, "userAgent", "get").mockReturnValue(ua);
}

const chromeUa = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/153.0 Safari/537.36";
const iphoneUa =
  "Mozilla/5.0 (iPhone; CPU iPhone OS 26_0 like Mac OS X) AppleWebKit/605.1.15 Mobile";

describe("InstallHint", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("shows nothing in a browser that can't install and isn't iOS", () => {
    setUserAgent(chromeUa);

    renderHint();

    expect(screen.queryByText("Install Wasatch Wargamers")).not.toBeInTheDocument();
  });

  it("offers Chrome's install prompt once the browser allows it", async () => {
    setUserAgent(chromeUa);
    renderHint();
    const prompt = vi.fn(() => Promise.resolve());
    const event = Object.assign(new Event("beforeinstallprompt", { cancelable: true }), { prompt });

    act(() => {
      window.dispatchEvent(event);
    });
    await userEvent.click(screen.getByRole("button", { name: "Install" }));

    expect(event.defaultPrevented).toBe(true);
    expect(prompt).toHaveBeenCalledOnce();
  });

  it("explains Add to Home Screen on iOS", () => {
    setUserAgent(iphoneUa);

    renderHint();

    expect(screen.getByText(/Tap the Share button, then Add to Home Screen/)).toBeInTheDocument();
  });

  it("stays dismissed after Not now", async () => {
    setUserAgent(iphoneUa);
    renderHint();

    await userEvent.click(screen.getByRole("button", { name: "Not now" }));

    expect(screen.queryByText("Install Wasatch Wargamers")).not.toBeInTheDocument();
    expect(localStorage.getItem("wwg:install-hint-dismissed")).toBe("true");
  });

  it("shows nothing once the app is installed", () => {
    setUserAgent(iphoneUa);
    const matchMedia = window.matchMedia.bind(window);
    vi.spyOn(window, "matchMedia").mockImplementation((query) =>
      Object.assign(matchMedia(query), { matches: query === "(display-mode: standalone)" }),
    );

    renderHint();

    expect(screen.queryByText("Install Wasatch Wargamers")).not.toBeInTheDocument();
  });
});
