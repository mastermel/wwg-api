import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { MapLegend } from "@/features/maps/MapLegend";

const renderLegend = (collapsible: boolean) =>
  render(
    <AppProviders queryClient={createQueryClient()}>
      <MapLegend umpire={false} collapsible={collapsible} />
    </AppProviders>,
  );

describe("the map's legend", () => {
  it("shows every group at once, on a phone", () => {
    renderLegend(false);

    expect(screen.getByRole("list", { name: "Terrain" })).toBeVisible();
    expect(screen.queryByRole("button", { name: "Terrain" })).not.toBeInTheDocument();
  });

  it("beside the map, opens one group at a time, the units first", async () => {
    const user = userEvent.setup();
    renderLegend(true);

    const units = screen.getByRole("button", { name: "Units" });
    const terrain = screen.getByRole("button", { name: "Terrain" });
    expect(units).toHaveAttribute("aria-expanded", "true");
    expect(terrain).toHaveAttribute("aria-expanded", "false");

    await user.click(terrain);

    expect(terrain).toHaveAttribute("aria-expanded", "true");
    expect(units).toHaveAttribute("aria-expanded", "false");
  });
});
