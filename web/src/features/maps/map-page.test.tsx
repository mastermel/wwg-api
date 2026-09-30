import { onlineManager } from "@tanstack/react-query";
import { act, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import type { CampaignMapResponse, CampaignResponse } from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";

// MapLibre needs WebGL, which jsdom lacks: the map is a stand-in that shows what it was given.
vi.mock("@/features/maps/CampaignMap", () => ({
  // Its children (the unit markers) need a real map, so they aren't drawn; clicking "the map"
  // clicks at a fixed point.
  CampaignMap: ({
    bounds,
    settings,
    onMapClick,
  }: {
    bounds: CampaignMapResponse["bounds"];
    settings: CampaignMapResponse;
    onMapClick?: (point: { longitude: number; latitude: number }) => void;
  }) => (
    <div role="application" aria-label="Campaign map">
      {JSON.stringify({ bounds, language: settings.labelLanguage })}
      <button type="button" onClick={() => onMapClick?.({ longitude: 4.4, latitude: 50.7 })}>
        Click the map
      </button>
    </div>
  ),
}));

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const settings = (bounds: CampaignMapResponse["bounds"]): CampaignMapResponse => ({
  bounds,
  labelLanguage: "fr",
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

  it("lists the hex details shown to the viewer", async () => {
    serveCampaign("Player", settings({ west: 4.2, south: 50.6, east: 4.6, north: 50.8 }));
    server.use(
      http.get(`*/api/campaigns/${campaignId}/grid/details`, () =>
        HttpResponse.json([
          {
            q: 1,
            r: -2,
            relief: "Hilly",
            features: {
              scrub: false,
              village: false,
              woods: false,
              forest: false,
              farms: true,
              fields: true,
              streams: true,
            },
            dominant: "StrongFarmhouse",
            favorability: "NotRolled",
            dice: { red: 6, white: 5, green: null },
            forArmyId: null,
            shownToArmyIds: [],
            shownToAll: false,
          },
        ]),
      ),
    );

    await renderApp(`/campaigns/${campaignId}/map`);

    const details = within(await screen.findByRole("region", { name: "Hex details" }));
    expect(details.getByText("Hex (1, −2)")).toBeInTheDocument();
    expect(
      details.getByText("Hilly: farms, fields and streams. A strong farmhouse."),
    ).toBeInTheDocument();
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

  describe("setting up", () => {
    const waterloo = { west: 4.2, south: 50.55, east: 4.7, north: 50.8 };
    const armyId = "0192f5c1-0000-7000-8000-00000000a001";
    const unitId = "0192f5c1-0000-7000-8000-00000000b001";

    function serveSetup(startProblems: string[]) {
      const requests: { method: string; url: string; body: unknown }[] = [];
      server.use(
        http.get(`*/api/campaigns/${campaignId}/armies`, () =>
          HttpResponse.json([
            {
              id: armyId,
              name: "Armée du Nord",
              commander: null,
              side: null,
              color: "Blue",
              nation: "France",
            },
          ]),
        ),
        http.get(`*/api/campaigns/${campaignId}/units`, () =>
          HttpResponse.json([
            {
              id: unitId,
              armyId,
              name: "Imperial Guard",
              type: "LineInfantry",
              fightingFactor: 6,
              points: 30,
            },
          ]),
        ),
        http.get(`*/api/campaigns/${campaignId}/turns`, () =>
          HttpResponse.json({ stage: "Setup", openTurn: 0, turns: [], startProblems }),
        ),
        http.put(`*/api/army-units/${unitId}/placement`, async ({ request }) => {
          requests.push({ method: "PUT", url: request.url, body: await request.json() });
          return HttpResponse.json({
            unitId,
            armyId,
            turn: 0,
            status: "Draft",
            kind: "Move",
            latitude: 50.7,
            longitude: 4.4,
          });
        }),
        http.post(`*/api/campaigns/${campaignId}/start`, ({ request }) => {
          requests.push({ method: "POST", url: request.url, body: null });
          return HttpResponse.json({ stage: "Running", openTurn: 1, turns: [], startProblems: [] });
        }),
      );
      return requests;
    }

    it("lets the Umpire place a unit by clicking the map", async () => {
      serveCampaign("Umpire", settings(waterloo));
      const requests = serveSetup(["Place 1 unit on the map."]);
      const user = userEvent.setup();
      await renderApp(`/campaigns/${campaignId}/map`);

      expect(await screen.findByText("Place 1 unit on the map.")).toBeInTheDocument();
      expect(screen.getByText("Not placed yet")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Start campaign" })).toBeDisabled();
      await user.click(screen.getByRole("button", { name: "Place Imperial Guard" }));
      expect(screen.getByText(/Click the map where/)).toHaveTextContent("Imperial Guard");
      await user.click(screen.getByRole("button", { name: "Click the map" }));

      expect(await screen.findByText("Placed Imperial Guard.")).toBeInTheDocument();
      expect(requests).toEqual([
        {
          method: "PUT",
          url: expect.stringContaining(unitId) as unknown,
          // The stand-in map's click, (4.4, 50.7), is in hex (-1, 0) of this area's grid.
          body: { q: -1, r: 0 },
        },
      ]);
      expect(screen.queryByText(/Click the map where/)).not.toBeInTheDocument();
    });

    it("starts the campaign after confirming, once nothing stops it", async () => {
      serveCampaign("Umpire", settings(waterloo));
      const requests = serveSetup([]);
      const user = userEvent.setup();
      await renderApp(`/campaigns/${campaignId}/map`);

      await user.click(await screen.findByRole("button", { name: "Start campaign" }));
      await user.click(
        within(await screen.findByRole("dialog")).getByRole("button", { name: "Start campaign" }),
      );

      expect(
        await screen.findByText("The campaign has started: turn 1 is open."),
      ).toBeInTheDocument();
      expect(requests.map((r) => r.method)).toEqual(["POST"]);
    });

    it("tells a Player the Umpire is setting up", async () => {
      serveCampaign("Player", settings(waterloo));
      serveSetup(["Place 1 unit on the map."]);

      await renderApp(`/campaigns/${campaignId}/map`);

      expect(await screen.findByText(/The Umpire is placing the armies/)).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Start campaign" })).not.toBeInTheDocument();
      expect(screen.queryByText("Place 1 unit on the map.")).not.toBeInTheDocument();
    });

    it("shows how far the open turn has got, once running", async () => {
      serveCampaign("Player", settings(waterloo));
      server.use(
        http.get(`*/api/campaigns/${campaignId}/turns`, () =>
          HttpResponse.json({
            stage: "Running",
            openTurn: 1,
            startProblems: [],
            turns: [
              {
                number: 0,
                openedAt: "2026-09-01T12:00:00Z",
                closedAt: "2026-09-02T12:00:00Z",
                submitted: 2,
                armies: 2,
                armyTurns: [],
              },
              {
                number: 1,
                openedAt: "2026-09-02T12:00:00Z",
                closedAt: null,
                submitted: 1,
                armies: 2,
                armyTurns: [],
              },
            ],
          }),
        ),
      );

      await renderApp(`/campaigns/${campaignId}/map`);

      expect(await screen.findByRole("heading", { level: 2, name: "Turn 1" })).toBeInTheDocument();
      expect(screen.getByText("1 of 2 armies have submitted this turn.")).toBeInTheDocument();
    });
  });
});
