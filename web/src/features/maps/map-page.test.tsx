import { onlineManager } from "@tanstack/react-query";
import { act, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import type { CampaignMapResponse, CampaignResponse } from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";

// MapLibre needs WebGL, which jsdom lacks: the map is a stand-in that shows what it was given.
vi.mock("@/features/maps/CampaignMap", () => ({
  CampaignMap: ({
    bounds,
    settings,
  }: {
    bounds: CampaignMapResponse["bounds"];
    settings: CampaignMapResponse;
  }) => (
    <div role="application" aria-label="Campaign map">
      {JSON.stringify({ bounds, language: settings.labelLanguage })}
    </div>
  ),
}));

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const settings = (bounds: CampaignMapResponse["bounds"]): CampaignMapResponse => ({
  bounds,
  labelLanguage: "fr",
  distanceUnit: "Kilometres",
  layers: { roads: true, places: true, water: true, forests: true, hills: true, contours: false },
  movementLimits: [],
});

function serveCampaign(myRole: CampaignResponse["myRole"], map: CampaignMapResponse) {
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Hundred Days",
        description: null,
        umpire: null,
        myRole,
        playerCount: 0,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      } satisfies CampaignResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/map`, () => HttpResponse.json(map)),
  );
}

describe("the map page", () => {
  it("shows the campaign's map, framed on its area", async () => {
    const waterloo = { west: 4.2, south: 50.55, east: 4.7, north: 50.8 };
    serveCampaign("Player", settings(waterloo));

    await renderApp(`/campaigns/${campaignId}/map`);

    expect(await screen.findByRole("application", { name: "Campaign map" })).toHaveTextContent(
      JSON.stringify({ bounds: waterloo, language: "fr" }),
    );
    expect(screen.getByRole("heading", { level: 1, name: "Map" })).toBeInTheDocument();
  });

  it("tells a Player the Umpire hasn't chosen the area yet", async () => {
    serveCampaign("Player", settings(null));

    await renderApp(`/campaigns/${campaignId}/map`);

    expect(
      await screen.findByText("The Umpire hasn't chosen the campaign's area yet."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("application")).not.toBeInTheDocument();
  });

  it("says it needs a connection when offline", async () => {
    serveCampaign("Player", settings(null));
    await renderApp(`/campaigns/${campaignId}/map`);
    await screen.findByRole("heading", { level: 1, name: "Map" });

    act(() => {
      onlineManager.setOnline(false);
    });

    expect(
      await screen.findByRole("status", { name: "The map needs a connection" }),
    ).toBeInTheDocument();
  });

  it("is a button on the campaign's page, for every member", async () => {
    serveCampaign("Player", settings(null));

    await renderApp(`/campaigns/${campaignId}`);

    expect(await screen.findByRole("link", { name: "Map" })).toHaveAttribute(
      "href",
      `/campaigns/${campaignId}/map`,
    );
  });
});
