import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import type {
  CampaignGridResponse,
  CampaignMapResponse,
  CampaignResponse,
  HexDetailResponse,
} from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";

// MapLibre needs WebGL, which jsdom lacks: a stand-in with buttons that click the map a little
// north of the area's middle (hex 0, 0, by its north side), or outside the grid.
vi.mock("@/features/maps/CampaignMap", () => ({
  CampaignMap: ({
    onMapClick,
    grid,
  }: {
    onMapClick?: (point: { longitude: number; latitude: number }) => void;
    grid?: boolean;
  }) => (
    <div role="application" aria-label="Campaign map">
      <p>Grid {grid === false ? "off" : "on"}</p>
      <button type="button" onClick={() => onMapClick?.({ longitude: 4.4, latitude: 50.71 })}>
        Click the middle
      </button>
      <button type="button" onClick={() => onMapClick?.({ longitude: 9, latitude: 50.7 })}>
        Click outside
      </button>
    </div>
  ),
}));

// The map's tiles, without the network: one vector tile with a city at the area's middle.
vi.mock("@/features/maps/inference/tiles", async () => {
  const { tileAt } = await import("@/features/maps/inference/sources");
  const middle = { longitude: 4.4, latitude: 50.7 };
  const { x, y } = tileAt(middle, 10);
  return {
    loadTiles: (
      _bounds: unknown,
      _size: number,
      onProgress: (done: number, total: number) => void,
    ) => {
      onProgress(1, 1);
      return Promise.resolve({
        vectorTiles: [
          {
            x,
            y,
            z: 10,
            features: [
              {
                layer: "place",
                properties: { class: "city", name: "Waterloo", capital: 0 },
                geometry: { type: "Point", coordinates: [middle.longitude, middle.latitude] },
              },
            ],
          },
        ],
        elevationTiles: [],
      });
    },
  };
});

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
    http.put(`*/api/campaigns/${campaignId}/grid`, async ({ request }) => {
      saved.push({ path: "", body: await request.json() });
      return new HttpResponse(null, { status: 204 });
    }),
    http.put(`*/api/campaigns/${campaignId}/grid/*`, async ({ request }) => {
      const body = await request.json();
      saved.push({ path: new URL(request.url).pathname.split("/grid/")[1] ?? "", body });
      return HttpResponse.json({});
    }),
  );
  return saved;
}

const page = `/campaigns/${campaignId}/map/terrain`;
const armyId = "0192f5c1-0000-7000-8000-00000000a001";

const rolled: HexDetailResponse = {
  q: 0,
  r: 0,
  relief: "Rolling",
  features: {
    scrub: false,
    village: true,
    woods: true,
    forest: false,
    farms: false,
    fields: false,
    streams: false,
  },
  dominant: "SmallCastle",
  favorability: "Favorable",
  dice: { red: 4, white: 1, green: 1 },
  forArmyId: armyId,
  shownToArmyIds: [],
  shownToAll: false,
};

/** The hexes' actual terrain, changing as the test rolls, saves and forgets; records the calls. */
function serveDetails(initial: HexDetailResponse[]) {
  let details = initial;
  const calls: { method: string; body: unknown }[] = [];
  server.use(
    http.get(`*/api/campaigns/${campaignId}/armies`, () =>
      HttpResponse.json([
        {
          id: armyId,
          name: "First Corps",
          commander: null,
          side: null,
          color: "Red",
          nation: "None",
        },
      ]),
    ),
    http.get(`*/api/campaigns/${campaignId}/grid/details`, () => HttpResponse.json(details)),
    http.post(`*/api/campaigns/${campaignId}/grid/details/0/0/roll`, async ({ request }) => {
      calls.push({ method: "POST", body: await request.json() });
      details = [rolled];
      return HttpResponse.json(rolled);
    }),
    http.put(`*/api/campaigns/${campaignId}/grid/details/0/0`, async ({ request }) => {
      const body = (await request.json()) as Partial<HexDetailResponse>;
      calls.push({ method: "PUT", body });
      details = [{ ...rolled, ...body }];
      return HttpResponse.json(details[0]);
    }),
    http.delete(`*/api/campaigns/${campaignId}/grid/details/0/0`, () => {
      calls.push({ method: "DELETE", body: null });
      details = [];
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return calls;
}

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

  it("lets the Umpire give a settlement its points and who holds it", async () => {
    const town = {
      q: 0,
      r: 0,
      terrain: "Flat" as const,
      forest: false,
      settlement: {
        size: "Town" as const,
        walled: false,
        fortress: false,
        capital: "None" as const,
        name: "Wavre",
        victoryPoints: null,
      },
      setByUmpire: true,
    };
    serveCampaign("Umpire", { cells: [town], edges: [] });
    const holdings: unknown[] = [];
    server.use(
      http.get(`*/api/campaigns/${campaignId}/armies`, () =>
        HttpResponse.json([
          {
            id: armyId,
            name: "Armée du Nord",
            commander: null,
            side: { id: "0192f5c1-0000-7000-8000-00000000f001", name: "French Empire" },
            color: "Blue",
            nation: "France",
          },
        ]),
      ),
      http.get(`*/api/campaigns/${campaignId}/scoreboard`, () =>
        HttpResponse.json({ sides: [], settlements: [], turns: [], changes: [] }),
      ),
      http.put(`*/api/campaigns/${campaignId}/holdings/0/0`, async ({ request }) => {
        holdings.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));
    const hex = within(screen.getByRole("region", { name: "Hex (0, 0)" }));
    // A town: the rules' 10, unless the Umpire says otherwise.
    expect(hex.getByRole("textbox", { name: "Victory points" })).toHaveAttribute(
      "placeholder",
      "10",
    );
    await user.click(await hex.findByRole("combobox", { name: "Held by" }));
    await user.click(await hex.findByRole("option", { name: "Armée du Nord", hidden: true }));

    expect(await screen.findByText("Hex (0, 0) is held by Armée du Nord.")).toBeInTheDocument();
    expect(holdings).toEqual([{ armyId }]);
  });

  it("gives a settlement no points of its own where only those the Umpire gives points count", async () => {
    serveCampaign("Umpire", {
      cells: [
        {
          q: 0,
          r: 0,
          terrain: "Flat",
          forest: false,
          settlement: {
            size: "Town",
            walled: false,
            fortress: false,
            capital: "None",
            name: "Wavre",
            victoryPoints: null,
          },
          setByUmpire: true,
        },
      ],
      edges: [],
    });
    server.use(
      http.get(`*/api/campaigns/${campaignId}/victory-settings`, () =>
        HttpResponse.json({ mode: "Chosen" }),
      ),
    );
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));
    const hex = within(screen.getByRole("region", { name: "Hex (0, 0)" }));
    const points = hex.getByRole("textbox", { name: "Victory points" });
    await waitFor(() => {
      expect(points).toHaveAttribute("placeholder", "0");
    });
    expect(points).toHaveAccessibleDescription(
      "What holding it is worth. Only the settlements you give points count.",
    );
    expect(hex.queryByRole("combobox", { name: "Held by" })).not.toBeInTheDocument();
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
            victoryPoints: null,
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

  it("infers the terrain from the map's data and saves it", async () => {
    const saved = serveCampaign("Umpire");
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Infer terrain" }));

    expect(await screen.findByText(/^Inferred the terrain: 1 hex and 0 edges/)).toBeInTheDocument();
    expect(saved).toEqual([
      {
        path: "",
        body: {
          cells: [
            {
              q: 0,
              r: 0,
              terrain: "Flat",
              forest: false,
              settlement: {
                size: "City",
                walled: false,
                fortress: false,
                capital: "None",
                name: "Waterloo",
              },
            },
          ],
          edges: [],
        },
      },
    ]);
  });

  it("asks before inferring again over what was inferred", async () => {
    const saved = serveCampaign("Umpire", {
      cells: [
        {
          q: 1,
          r: 0,
          terrain: "LowHill",
          forest: false,
          settlement: { size: "None", walled: false, fortress: false, capital: "None", name: null },
          setByUmpire: false,
        },
      ],
      edges: [],
    });
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Infer again" }));
    const dialog = within(await screen.findByRole("dialog", { name: "Infer the terrain again?" }));
    await user.click(dialog.getByRole("button", { name: "Infer again" }));

    expect(await screen.findByText(/^Inferred the terrain/)).toBeInTheDocument();
    expect(saved).toHaveLength(1);
  });

  it("lets the Umpire roll a hex's actual terrain for an army, and shows what the dice found", async () => {
    serveCampaign("Umpire");
    const calls = serveDetails([]);
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));
    const section = within(screen.getByRole("region", { name: "Actual terrain" }));
    await user.click(section.getByRole("combobox", { name: "Asked by" }));
    await user.click(await section.findByRole("option", { name: "First Corps", hidden: true }));
    await user.click(section.getByRole("switch", { name: "Roll favourability" }));
    await user.click(section.getByRole("button", { name: "Roll the dice" }));

    expect(
      await screen.findByText(
        "The dice found: Rolling: a small village and small woods. A small castle. Favourable ground.",
      ),
    ).toBeInTheDocument();
    expect(calls).toEqual([
      { method: "POST", body: { forArmyId: armyId, favorability: true, flatMinusOne: false } },
    ]);
    // Now it's there to change.
    expect(await section.findByText("Red 4, white 1, green 1.")).toBeInTheDocument();
    expect(section.getByRole("button", { name: "Roll again" })).toBeInTheDocument();
  });

  it("lets the Umpire change what was found and show it to an army", async () => {
    serveCampaign("Umpire");
    const calls = serveDetails([rolled]);
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));
    const section = within(screen.getByRole("region", { name: "Actual terrain" }));
    await user.click(await section.findByRole("checkbox", { name: "Streams" }));
    const shownTo = section.getByRole("combobox", { name: "Shown to" });
    await user.click(shownTo);
    // "Asked by" lists the armies too: the option in this field's own list.
    const list = document.getElementById(shownTo.getAttribute("aria-controls") ?? "");
    await user.click(
      within(list ?? document.body).getByRole("option", { name: "First Corps", hidden: true }),
    );
    await user.click(section.getByRole("button", { name: "Save actual terrain" }));

    expect(await screen.findByText("Saved the actual terrain.")).toBeInTheDocument();
    expect(calls).toEqual([
      {
        method: "PUT",
        body: {
          relief: "Rolling",
          features: {
            scrub: false,
            village: true,
            woods: true,
            forest: false,
            farms: false,
            fields: false,
            streams: true,
          },
          dominant: "SmallCastle",
          favorability: "Favorable",
          forArmyId: armyId,
          shownToArmyIds: [armyId],
          shownToAll: false,
        },
      },
    ]);
  });

  it("offers one off the red die only on a flat hex", async () => {
    serveCampaign("Umpire", {
      cells: [
        {
          q: 0,
          r: 0,
          terrain: "LowHill",
          forest: false,
          settlement: { size: "None", walled: false, fortress: false, capital: "None", name: null },
          setByUmpire: true,
        },
      ],
      edges: [],
    });
    serveDetails([]);
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));

    const section = within(screen.getByRole("region", { name: "Actual terrain" }));
    expect(section.queryByRole("switch", { name: "One off the red die" })).not.toBeInTheDocument();
  });

  it("lets the Umpire forget it, once confirmed", async () => {
    serveCampaign("Umpire");
    const calls = serveDetails([rolled]);
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Click the middle" }));
    await user.click(
      await within(screen.getByRole("region", { name: "Actual terrain" })).findByRole("button", {
        name: "Forget",
      }),
    );
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Forget" }),
    );

    expect(await screen.findByText("Forgot the actual terrain.")).toBeInTheDocument();
    expect(calls.map((c) => c.method)).toEqual(["DELETE"]);
  });

  it("lets the Umpire hide game map layers while editing, remembered apart from the Map page", async () => {
    localStorage.clear();
    serveCampaign("Umpire", { cells: [], edges: [] });
    const user = userEvent.setup();
    await renderApp(page);

    await user.click(await screen.findByRole("button", { name: "Map layers" }));
    const game = await screen.findByRole("group", { name: "Game map", hidden: true });
    expect(within(game).queryByText(/Zoom in to see it/)).not.toBeInTheDocument();
    expect(
      within(game).queryByRole("switch", { name: "Contact & concentration", hidden: true }),
    ).not.toBeInTheDocument();
    await user.click(within(game).getByRole("switch", { name: "Grid", hidden: true }));

    expect(screen.getByText("Grid off")).toBeInTheDocument();
    expect(localStorage.getItem(`wwg:terrain-layers:${campaignId}`)).toContain("grid");
    expect(localStorage.getItem(`wwg:map-layers:${campaignId}`)).toBeNull();
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
