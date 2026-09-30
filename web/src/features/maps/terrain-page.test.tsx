import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import type {
  CampaignGridResponse,
  CampaignMapResponse,
  CampaignResponse,
} from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";

// MapLibre needs WebGL, which jsdom lacks: a stand-in with buttons that click the map a little
// north of the area's middle (hex 0, 0, by its north side), or outside the grid.
vi.mock("@/features/maps/CampaignMap", () => ({
  CampaignMap: ({
    onMapClick,
  }: {
    onMapClick?: (point: { longitude: number; latitude: number }) => void;
  }) => (
    <div role="application" aria-label="Campaign map">
      <button type="button" onClick={() => onMapClick?.({ longitude: 4.4, latitude: 50.71 })}>
        Click the middle
      </button>
      <button type="button" onClick={() => onMapClick?.({ longitude: 9, latitude: 50.7 })}>
        Click outside
      </button>
    </div>
  ),
}));

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const waterloo = { west: 4.2, south: 50.6, east: 4.6, north: 50.8 };

const settings = (bounds: CampaignMapResponse["bounds"]): CampaignMapResponse => ({
  bounds,
  labelLanguage: "en",
  distanceUnit: "Kilometres",
  layers: {
    roads: true,
    places: true,
    water: true,
    forests: true,
    hills: true,
    contours: false,
    grid: true,
  },
  hexSize: 4828,
});

/** A campaign with this terrain; records what's saved. */
function serveCampaign(
  myRole: CampaignResponse["myRole"],
  terrain: CampaignGridResponse = { cells: [], edges: [] },
  bounds: CampaignMapResponse["bounds"] = waterloo,
) {
  const saved: { path: string; body: unknown }[] = [];
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
    http.get(`*/api/campaigns/${campaignId}/map`, () => HttpResponse.json(settings(bounds))),
    http.get(`*/api/campaigns/${campaignId}/grid`, () => HttpResponse.json(terrain)),
    http.put(`*/api/campaigns/${campaignId}/grid/*`, async ({ request }) => {
      const body = await request.json();
      saved.push({ path: new URL(request.url).pathname.split("/grid/")[1] ?? "", body });
      return HttpResponse.json({});
    }),
  );
  return saved;
}

const page = `/campaigns/${campaignId}/map/terrain`;

describe("the terrain page", () => {
  it("asks the Umpire to choose a hex, and says when it's outside the grid", async () => {
    serveCampaign("Umpire");
    const user = userEvent.setup();
    const { container } = await renderApp(page);

    expect(await screen.findByText(/Click a hex on the map/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Click outside" }));

    expect(screen.getByText(/That's outside the campaign's grid/)).toBeInTheDocument();
    await expectNoAxeViolations(container);
  });

  it("lets the Umpire set a hex's ground and settlement", async () => {
    const saved = serveCampaign("Umpire");
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));
    const hex = within(screen.getByRole("region", { name: "Hex (0, 0)" }));
    expect(hex.getByText("Flat, with nothing on it.")).toBeInTheDocument();
    expect(hex.getByRole("switch", { name: "Walled" })).toBeDisabled();
    await user.click(hex.getByRole("combobox", { name: "Ground" }));
    await user.click(await hex.findByRole("option", { name: "Low hills", hidden: true }));
    await user.click(hex.getByRole("combobox", { name: "Town or city" }));
    await user.click(await hex.findByRole("option", { name: "Town", hidden: true }));
    await user.click(hex.getByRole("switch", { name: "Walled" }));
    await user.type(hex.getByRole("textbox", { name: "Name" }), " Mont-Saint-Jean ");
    await user.click(hex.getByRole("button", { name: "Save hex" }));

    expect(await screen.findByText("Saved Hex (0, 0).")).toBeInTheDocument();
    expect(saved).toEqual([
      {
        path: "cells/0/0",
        body: {
          terrain: "LowHill",
          forest: false,
          settlement: {
            size: "Town",
            walled: true,
            fortress: false,
            capital: "None",
            name: "Mont-Saint-Jean",
          },
        },
      },
    ]);
  });

  it("lets the Umpire set an edge, saved where it's stored, with its flow turned round", async () => {
    const saved = serveCampaign("Umpire");
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));
    const edge = within(screen.getByRole("region", { name: "Edge" }));
    expect(edge.getByRole("combobox", { name: "Side" })).toHaveValue("North");
    await user.click(edge.getByRole("combobox", { name: "Side" }));
    await user.click(await edge.findByRole("option", { name: "South", hidden: true }));
    // A new form for the south edge.
    const south = within(screen.getByRole("region", { name: "Edge" }));
    await user.click(south.getByRole("switch", { name: "River along it" }));
    await user.click(south.getByRole("switch", { name: "Bridge" }));
    await user.click(south.getByRole("combobox", { name: "Road across it" }));
    await user.click(await south.findByRole("option", { name: "Good road", hidden: true }));
    await user.click(south.getByRole("combobox", { name: "Waterway across it" }));
    await user.click(
      await south.findByRole("option", { name: "Flows into this hex", hidden: true }),
    );
    await user.click(south.getByRole("button", { name: "Save edge" }));

    expect(await screen.findByText("Saved the south edge.")).toBeInTheDocument();
    // Hex (0, 0)'s south is hex (0, 1)'s north: flowing into (0, 0) is out of (0, 1).
    expect(saved).toEqual([
      {
        path: "edges/0/1/N",
        body: { road: "Good", river: true, bridge: true, waterway: "Out" },
      },
    ]);
  });

  it("shows what a hex and edge have, and which the Umpire set", async () => {
    serveCampaign("Umpire", {
      cells: [
        {
          q: 0,
          r: 0,
          terrain: "HighHill",
          forest: true,
          settlement: {
            size: "City",
            walled: true,
            fortress: false,
            capital: "Minor",
            name: "Namur",
          },
          setByUmpire: true,
        },
      ],
      edges: [
        {
          q: 0,
          r: 0,
          side: "N",
          road: "Poor",
          river: false,
          bridge: false,
          waterway: "None",
          setByUmpire: false,
        },
      ],
    });
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));

    const hex = within(screen.getByRole("region", { name: "Hex (0, 0)" }));
    expect(
      hex.getByText("High hills, forest, Walled minor capital city: Namur"),
    ).toBeInTheDocument();
    expect(hex.getByText("Set by you")).toBeInTheDocument();
    expect(hex.getByRole("textbox", { name: "Name" })).toHaveValue("Namur");
    const edge = within(screen.getByRole("region", { name: "Edge" }));
    expect(edge.getByRole("combobox", { name: "Road across it" })).toHaveValue("Poor road");
    expect(edge.queryByText("Set by you")).not.toBeInTheDocument();
  });

  it("shows the API's reason when a save is refused", async () => {
    serveCampaign("Umpire");
    server.use(
      http.put(`*/api/campaigns/${campaignId}/grid/cells/*`, () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            errors: { settlement: ["Only a town or city can be walled or a capital."] },
          },
          { status: 400, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));
    await user.click(screen.getByRole("button", { name: "Save hex" }));

    expect(
      await screen.findByText("Only a town or city can be walled or a capital."),
    ).toBeInTheDocument();
  });

  it("isn't for Players", async () => {
    serveCampaign("Player");

    await renderApp(page);

    expect(
      await screen.findByRole("status", { name: "Only the Umpire can change the terrain" }),
    ).toBeInTheDocument();
  });

  it("asks for the map's area first", async () => {
    serveCampaign("Umpire", undefined, null);

    await renderApp(page);

    expect(await screen.findByText("No map yet")).toBeInTheDocument();
  });

  it("is a button on the map page, for the Umpire once there's an area", async () => {
    serveCampaign("Umpire");
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map`);

    await user.click(await screen.findByRole("link", { name: "Terrain" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Terrain" })).toBeInTheDocument();
  });
});
