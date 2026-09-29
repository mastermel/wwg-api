import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { useImperativeHandle, type Ref } from "react";
import { describe, expect, it, vi } from "vitest";
import type { CampaignMapResponse, CampaignResponse, PlaceResult } from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";

// MapLibre needs WebGL, which jsdom lacks. The stand-in map is "looking at" Leipzig, and records
// where the page asks it to go.
const flights = vi.hoisted(() => ({ fitBounds: [] as unknown[] }));
vi.mock("@/features/maps/CampaignMap", () => ({
  CampaignMap: ({ mapRef }: { mapRef?: Ref<unknown> }) => {
    useImperativeHandle(mapRef, () => ({
      getBounds: () => ({
        getWest: () => 12.2,
        getSouth: () => 51.2,
        getEast: () => 12.6,
        getNorth: () => 51.5,
      }),
      fitBounds: (bounds: unknown) => flights.fitBounds.push(bounds),
      flyTo: () => undefined,
    }));
    return <div role="application" aria-label="Campaign map" />;
  },
}));

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const saved: CampaignMapResponse = {
  bounds: null,
  labelLanguage: "en",
  distanceUnit: "Kilometres",
  layers: { roads: true, places: true, water: true, forests: true, hills: true, contours: false },
  movementLimits: [
    { unitType: "HeavyInfantry", metres: 20_000 },
    { unitType: "LightCavalry", metres: 40_000 },
  ],
};

const leipzig: PlaceResult = {
  name: "Leipzig",
  description: "Leipzig, Saxony, Germany",
  longitude: 12.37,
  latitude: 51.34,
  bounds: { west: 12.23, south: 51.23, east: 12.54, north: 51.45 },
};

function serve(myRole: CampaignResponse["myRole"]) {
  const puts: unknown[] = [];
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "Leipzig 1813",
        description: null,
        umpire: null,
        myRole,
        playerCount: 0,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      } satisfies CampaignResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/map`, () => HttpResponse.json(saved)),
    http.get(`*/api/campaigns/${campaignId}/places`, () => HttpResponse.json([leipzig])),
    http.put(`*/api/campaigns/${campaignId}/map`, async ({ request }) => {
      const body = (await request.json()) as object;
      puts.push(body);
      return HttpResponse.json({ ...saved, ...body });
    }),
  );
  return puts;
}

describe("map settings", () => {
  it("finds a place, uses the view as the area, and saves it all in metres", async () => {
    const puts = serve("Umpire");
    const user = userEvent.setup();
    const { router } = await renderApp(`/campaigns/${campaignId}/map/settings`);

    await user.type(await screen.findByRole("combobox", { name: "Find a place" }), "Leipzig");
    await user.click(
      await screen.findByRole("option", { name: "Leipzig, Saxony, Germany", hidden: true }),
    );
    expect(flights.fitBounds).toContainEqual([12.23, 51.23, 12.54, 51.45]);

    await user.click(screen.getByRole("button", { name: "Use this view" }));
    expect(screen.getByText("The outline is the campaign's area.")).toBeInTheDocument();
    await user.click(screen.getByRole("switch", { name: "Forests" }));
    const cavalry = screen.getByRole("textbox", { name: "Light Cavalry" });
    await user.clear(cavalry);
    await user.type(cavalry, "45");
    await user.click(screen.getByRole("button", { name: "Save map settings" }));

    expect(await screen.findByText("Saved the map settings.")).toBeInTheDocument();
    expect(puts).toEqual([
      {
        bounds: { west: 12.2, south: 51.2, east: 12.6, north: 51.5 },
        labelLanguage: "en",
        distanceUnit: "Kilometres",
        layers: { ...saved.layers, forests: false },
        movementLimits: [
          { unitType: "HeavyInfantry", metres: 20_000 },
          { unitType: "LightCavalry", metres: 45_000 },
        ],
      },
    ]);
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/campaigns/${campaignId}/map`);
    });
  });

  it("keeps the distances when switching to miles", async () => {
    serve("Umpire");
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map/settings`);

    await user.click(await screen.findByRole("radio", { name: "Miles" }));

    // 20 km and 40 km.
    expect(screen.getByRole("textbox", { name: "Heavy Infantry" })).toHaveValue("12.4 mi");
    expect(screen.getByRole("textbox", { name: "Light Cavalry" })).toHaveValue("24.9 mi");
  });

  it("tells a Player only the Umpire can change the map", async () => {
    serve("Player");

    await renderApp(`/campaigns/${campaignId}/map/settings`);

    expect(
      await screen.findByRole("status", { name: "Only the Umpire can change the map" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save map settings" })).not.toBeInTheDocument();
  });

  it("is a button on the map page for the Umpire, with nothing chosen yet", async () => {
    serve("Umpire");

    await renderApp(`/campaigns/${campaignId}/map`);

    await screen.findByText("No map yet");
    // The page's action, and the empty map's.
    expect(
      within(screen.getByRole("main")).getAllByRole("link", { name: "Map settings" }),
    ).toHaveLength(2);
  });
});
