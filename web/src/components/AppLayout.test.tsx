import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { server, serveHealth } from "@/test/server";
import { renderApp } from "@/test/render";

describe("app layout and routing", () => {
  it("sends the start page to the campaign list", async () => {
    await renderApp("/");

    expect(await screen.findByRole("heading", { level: 1, name: "Campaigns" })).toBeInTheDocument();
    expect(document.title).toBe("Campaigns · Wasatch Wargamers");
  });

  it("shows a not-found page for an unknown address", async () => {
    await renderApp("/no-such-page");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Page not found" }),
    ).toBeInTheDocument();
  });

  it("marks the current page in the navigation and moves focus to the new heading", async () => {
    server.use(serveHealth("Healthy"));
    const user = userEvent.setup();
    await renderApp("/campaigns");
    await screen.findByRole("heading", { level: 1, name: "Campaigns" });

    const sidebar = screen.getByRole("navigation", { name: "Main" });
    expect(within(sidebar).getByRole("link", { name: "Campaigns" })).toHaveAttribute(
      "aria-current",
      "page",
    );

    await user.click(within(sidebar).getByRole("link", { name: "About" }));

    const heading = await screen.findByRole("heading", { level: 1, name: "About" });
    await waitFor(() => {
      expect(heading).toHaveFocus();
    });
    expect(within(sidebar).getByRole("link", { name: "About" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });
});
