import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { useImperativeHandle, type Ref } from "react";
import { describe, expect, it, vi } from "vitest";
import type { CampaignMapResponse, CampaignResponse, PlaceResult } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";

// MapLibre needs WebGL, which jsdom lacks. The stand-in map is "looking at" Leipzig, and records
// where the page asks it to go.
const flights = vi.hoisted(() => ({ fitBounds: [] as unknown[] }));
vi.mock("@/features/maps/CampaignMap", () => ({
  // Dragging on it goes from Waterloo's north-west corner to its south-east (the map tests'
  // Waterloo: 31 hexes at 3 miles), as MapLibre would report it.
  CampaignMap: ({
    mapRef,
    onPointer,
  }: {
    mapRef?: Ref<unknown>;
    onPointer?: (event: {
      kind: "down" | "move" | "up";
      longitude: number;
      latitude: number;
      x: number;
      y: number;
    }) => void;
  }) => {
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
    return (
      <div role="application" aria-label="Campaign map">
        <button
          type="button"
          onClick={() => {
            onPointer?.({ kind: "down", longitude: 4.2, latitude: 50.8, x: 10, y: 10 });
            onPointer?.({ kind: "move", longitude: 4.4, latitude: 50.7, x: 100, y: 60 });
            onPointer?.({ kind: "up", longitude: 4.6, latitude: 50.6, x: 200, y: 110 });
          }}
        >
          Drag on the map
        </button>
      </div>
    );
  },
}));

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const saved: CampaignMapResponse = {
  bounds: null,
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
  it("finds a place, uses the view as the area, and saves it", async () => {
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
    await user.click(screen.getByRole("button", { name: "Save map settings" }));

    expect(await screen.findByText("Saved the map settings.")).toBeInTheDocument();
    expect(puts).toEqual([
      {
        bounds: { west: 12.2, south: 51.2, east: 12.6, north: 51.5 },
        labelLanguage: "en",
        distanceUnit: "Kilometres",
        layers: { ...saved.layers, forests: false },
        hexSize: 4828,
      },
    ]);
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/campaigns/${campaignId}/map`);
    });
  });

  it("draws the area as a rectangle on the map, and saves it", async () => {
    const puts = serve("Umpire");
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map/settings`);

    await user.click(await screen.findByRole("button", { name: "Draw the area" }));
    expect(screen.getByText(/Drag a rectangle on the map/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Drag on the map" }));

    expect(screen.getByText("The outline is the campaign's area.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Draw the area" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Save map settings" }));
    expect(await screen.findByText("Saved the map settings.")).toBeInTheDocument();
    expect(puts[0]).toMatchObject({ bounds: { west: 4.2, south: 50.6, east: 4.6, north: 50.8 } });
  });

  it("stops drawing without changing the area", async () => {
    serve("Umpire");
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map/settings`);

    await user.click(await screen.findByRole("button", { name: "Draw the area" }));
    await user.click(screen.getByRole("button", { name: "Stop drawing" }));
    await user.click(screen.getByRole("button", { name: "Drag on the map" }));

    expect(screen.getByText("No area chosen yet.")).toBeInTheDocument();
  });

  it("counts the area's hexes as it and the hex size change, while the grid's shown", async () => {
    serve("Umpire");
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map/settings`);
    await user.click(await screen.findByRole("button", { name: "Draw the area" }));
    await user.click(screen.getByRole("button", { name: "Drag on the map" }));

    expect(screen.getByText("31 hexes in the area.")).toBeInTheDocument();
    const size = screen.getByRole("textbox", { name: "Hex size, across the flats" });
    await user.clear(size);
    await user.type(size, "2");
    expect(await screen.findByText(/^1[,\d]* hexes in the area\.$/)).toBeInTheDocument();
    await user.click(screen.getByRole("switch", { name: "Hex grid" }));
    expect(screen.queryByText(/hexes in the area/)).not.toBeInTheDocument();
  });

  it("keeps the hex size when switching to miles", async () => {
    serve("Umpire");
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map/settings`);

    await user.click(await screen.findByRole("radio", { name: "Miles" }));

    expect(screen.getByRole("textbox", { name: "Hex size, across the flats" })).toHaveValue("3 mi");
  });

  it("saves the hex size in metres", async () => {
    const puts = serve("Umpire");
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map/settings`);

    const size = await screen.findByRole("textbox", { name: "Hex size, across the flats" });
    expect(size).toHaveValue("4.8 km");
    await user.clear(size);
    await user.type(size, "5");
    await user.click(screen.getByRole("button", { name: "Save map settings" }));

    expect(await screen.findByText("Saved the map settings.")).toBeInTheDocument();
    expect(puts).toEqual([expect.objectContaining({ hexSize: 5000 })]);
  });

  it("keeps the area and hex size fixed once the campaign has started", async () => {
    serve("Umpire");
    server.use(
      http.get(`*/api/campaigns/${campaignId}/turns`, () =>
        HttpResponse.json({ stage: "Running", openTurn: 1, turns: [], startProblems: [] }),
      ),
    );

    await renderApp(`/campaigns/${campaignId}/map/settings`);

    expect(
      await screen.findByText("The campaign has started: its area and grid are fixed."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Use this view" })).toBeDisabled();
    expect(screen.getByRole("textbox", { name: "Hex size, across the flats" })).toBeDisabled();
  });

  it("says when the area has too many hexes to draw", async () => {
    serve("Umpire");
    server.use(
      http.get(`*/api/campaigns/${campaignId}/map`, () =>
        HttpResponse.json({ ...saved, bounds: { west: -10, south: 36, east: 30, north: 60 } }),
      ),
    );

    await renderApp(`/campaigns/${campaignId}/map/settings`);

    expect(await screen.findByText(/too many to draw/)).toBeInTheDocument();
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

describe("the movement table", () => {
  /** The table, the rules' until saved; records what's sent. */
  function serveTable() {
    let own: { class: string; ground: string; hexes: number }[] = [];
    const sent: unknown[] = [];
    server.use(
      http.get(`*/api/campaigns/${campaignId}/movement`, () =>
        HttpResponse.json({ rates: own, rules: own.length === 0 }),
      ),
      http.put(`*/api/campaigns/${campaignId}/movement`, async ({ request }) => {
        const body = (await request.json()) as { rates: typeof own };
        sent.push(body);
        own = body.rates;
        return HttpResponse.json({ rates: own, rules: false });
      }),
      http.delete(`*/api/campaigns/${campaignId}/movement`, () => {
        sent.push("reset");
        own = [];
        return new HttpResponse(null, { status: 204 });
      }),
    );
    return sent;
  }

  it("shows the rule book's table, and lets the Umpire change a rate", async () => {
    serve("Umpire");
    const sent = serveTable();
    const user = userEvent.setup();
    const { container } = await renderApp(`/campaigns/${campaignId}/map/settings`);

    const table = within(await screen.findByRole("region", { name: "Movement" }));
    expect(await table.findByText("The rule book's")).toBeInTheDocument();
    const cell = table.getByRole("textbox", { name: "Infantry and foot artillery on high hills" });
    expect(cell).toHaveValue("0.5");
    expect(
      table.getByRole("textbox", { name: "Supply trains and siege artillery on mountains" }),
    ).toHaveValue("0");
    await expectNoAxeViolations(container);
    await user.clear(cell);
    await user.type(cell, "1");
    await user.click(table.getByRole("button", { name: "Save movement table" }));

    expect(await screen.findByText("Saved the movement table.")).toBeInTheDocument();
    const rates = (sent[0] as { rates: { class: string; ground: string; hexes: number }[] }).rates;
    // Five land classes on six grounds, and boats on three.
    expect(rates).toHaveLength(33);
    expect(rates).toContainEqual({ class: "Boat", ground: "Downstream", hexes: 4 });
    expect(rates).toContainEqual({ class: "Infantry", ground: "HighHill", hexes: 1 });
    expect(rates).toContainEqual({ class: "Slow", ground: "Flat", hexes: 1 });
    expect(await table.findByRole("button", { name: "Use the rule book's" })).toBeInTheDocument();
  });

  it("puts the rule book's table back, once confirmed", async () => {
    serve("Umpire");
    const sent = serveTable();
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map/settings`);
    const table = within(await screen.findByRole("region", { name: "Movement" }));
    await user.click(await table.findByRole("button", { name: "Save movement table" }));
    await user.click(await table.findByRole("button", { name: "Use the rule book's" }));

    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", {
        name: "Use the rule book's",
      }),
    );

    expect(await screen.findByText("Back to the rule book's movement table.")).toBeInTheDocument();
    expect(sent.at(-1)).toBe("reset");
  });

  it("waits for every rate before saving", async () => {
    serve("Umpire");
    serveTable();
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/map/settings`);
    const table = within(await screen.findByRole("region", { name: "Movement" }));

    await user.clear(
      await table.findByRole("textbox", { name: "Cavalry and horse artillery on flat" }),
    );

    expect(table.getByText("Give every one a number.")).toBeInTheDocument();
    expect(table.getByRole("button", { name: "Save movement table" })).toBeDisabled();
  });
});
