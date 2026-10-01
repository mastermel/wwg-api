import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import type {
  ArmySummary,
  CampaignMapResponse,
  CampaignResponse,
  DepotResponse,
} from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";

// MapLibre needs WebGL, which jsdom lacks: a stand-in whose button clicks the hex at (0, 0).
vi.mock("@/features/maps/CampaignMap", () => ({
  CampaignMap: ({
    onMapClick,
  }: {
    onMapClick?: (point: { longitude: number; latitude: number }) => void;
  }) => (
    <div role="application" aria-label="Campaign map">
      <button type="button" onClick={() => onMapClick?.({ longitude: 4.45, latitude: 50.675 })}>
        Click the map
      </button>
    </div>
  ),
}));

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const nord: ArmySummary = {
  id: "0192f5c1-0000-7000-8000-00000000a001",
  name: "Armée du Nord",
  commander: null,
  side: { id: "0192f5c1-0000-7000-8000-00000000f001", name: "French Empire" },
  color: "Blue",
  nation: "France",
};
const charleroi: DepotResponse = {
  id: "0192f5c1-0000-7000-8000-00000000d101",
  armyId: nord.id,
  kind: "Main",
  name: "Charleroi",
  q: 0,
  r: 0,
  latitude: 50.675,
  longitude: 4.45,
  cutOffTurns: 0,
};

function serve(myRole: CampaignResponse["myRole"], depots: DepotResponse[]) {
  const requests: { method: string; url: string; body: unknown }[] = [];
  const record = async (request: Request) => {
    const text = await request.text();
    requests.push({
      method: request.method,
      url: new URL(request.url).pathname,
      body: text ? (JSON.parse(text) as unknown) : null,
    });
  };
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Hundred Days",
        description: null,
        umpire: null,
        myRole,
        playerCount: 1,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      } satisfies CampaignResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/map`, () =>
      HttpResponse.json({
        bounds: { west: 4.2, south: 50.55, east: 4.7, north: 50.8 },
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
      } satisfies CampaignMapResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/armies`, () => HttpResponse.json([nord])),
    http.get(`*/api/campaigns/${campaignId}/turns`, () =>
      HttpResponse.json({ stage: "Setup", openTurn: 0, turns: [], startProblems: [] }),
    ),
    http.get(`*/api/campaigns/${campaignId}/depots`, () => HttpResponse.json(depots)),
    http.post(`*/api/armies/${nord.id}/depots`, async ({ request }) => {
      await record(request);
      return HttpResponse.json(charleroi, { status: 201 });
    }),
    http.put(`*/api/depots/${charleroi.id}`, async ({ request }) => {
      await record(request);
      return HttpResponse.json(charleroi);
    }),
    http.delete(`*/api/depots/${charleroi.id}`, async ({ request }) => {
      await record(request);
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return requests;
}

const openMap = () => renderApp(`/campaigns/${campaignId}/map`);

describe("depots", () => {
  it("lets the Umpire add one: its army, kind and name, then where on the map", async () => {
    const requests = serve("Umpire", []);
    const user = userEvent.setup();
    await openMap();

    const panel = await screen.findByRole("region", { name: "Depots" });
    expect(within(panel).getByText(/No depots yet/)).toBeInTheDocument();
    await user.click(within(panel).getByRole("button", { name: "Add depot" }));
    const dialog = await screen.findByRole("dialog", { name: "Add a depot" });
    await user.type(within(dialog).getByRole("textbox", { name: "Name" }), "Charleroi");
    await user.click(within(dialog).getByRole("button", { name: "Place it on the map" }));
    expect(screen.getByText(/Click the map where/)).toHaveTextContent("Charleroi");
    await user.click(screen.getByRole("button", { name: "Click the map" }));

    expect(await screen.findByText("Placed Charleroi.")).toBeInTheDocument();
    expect(requests).toEqual([
      {
        method: "POST",
        url: `/api/armies/${nord.id}/depots`,
        body: { kind: "Main", name: "Charleroi", q: 0, r: 0 },
      },
    ]);
  });

  it("lists them by army, and lets the Umpire move and remove one", async () => {
    const requests = serve("Umpire", [charleroi]);
    const user = userEvent.setup();
    await openMap();

    const list = await screen.findByRole("list", { name: "Armée du Nord's depots" });
    expect(list).toHaveTextContent("Charleroi · Main depot, Hex (0, 0)");
    await user.click(within(list).getByRole("button", { name: "Move Charleroi" }));
    await user.click(screen.getByRole("button", { name: "Click the map" }));
    expect(await screen.findByText("Saved Charleroi.")).toBeInTheDocument();
    await user.click(within(list).getByRole("button", { name: "Remove Charleroi" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Remove depot" }),
    );

    expect(await screen.findByText("Removed Charleroi.")).toBeInTheDocument();
    expect(requests.map((r) => r.method)).toEqual(["PUT", "DELETE"]);
  });

  it("shows a commander their army's depots, without changing them", async () => {
    serve("Player", [charleroi]);
    await openMap();

    const list = await screen.findByRole("list", { name: "Armée du Nord's depots" });
    expect(within(list).queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add depot" })).not.toBeInTheDocument();
  });

  it("shows a Player with no depots to see nothing", async () => {
    serve("Player", []);
    await openMap();

    await screen.findByRole("application", { name: "Campaign map" });
    expect(screen.queryByRole("region", { name: "Depots" })).not.toBeInTheDocument();
  });
});
