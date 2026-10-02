import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import type {
  ArmySummary,
  ArmyTurnDetails,
  ArmyUnitResponse,
  CampaignGridResponse,
  CampaignMapResponse,
  CampaignResponse,
  CampaignTurnsResponse,
  UnitPosition,
} from "@/api/generated/model";
import { noSettlement } from "@/features/maps/terrain";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

// In hex (0, -1), north of the Guard's (0, 0) in the middle of the area.
const click = vi.hoisted(() => ({ at: { longitude: 4.45, latitude: 50.71 } }));

// MapLibre needs WebGL, which jsdom lacks: the map is a stand-in whose button clicks `click.at`.
vi.mock("@/features/maps/CampaignMap", () => ({
  CampaignMap: ({
    onMapClick,
  }: {
    onMapClick?: (point: { longitude: number; latitude: number }) => void;
  }) => (
    <div role="application" aria-label="Campaign map">
      <button type="button" onClick={() => onMapClick?.(click.at)}>
        Click the map
      </button>
    </div>
  ),
}));

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const armyId = "0192f5c1-0000-7000-8000-00000000a001";
const guardId = "0192f5c1-0000-7000-8000-00000000b001";
const turnId = "0192f5c1-0000-7000-8000-00000000d001";
const boatIds = ["b1", "b2", "b3"].map((b) => `0192f5c1-0000-7000-8000-0000000000${b}`);

const army: ArmySummary = {
  id: armyId,
  name: "Armée du Nord",
  commander: {
    memberId: "0192f5c1-0000-7000-8000-00000000e001",
    userId: testUser.id,
    firstName: testUser.firstName,
    lastName: testUser.lastName,
  },
  side: { id: "0192f5c1-0000-7000-8000-00000000f001", name: "French Empire" },
  color: "Blue",
  nation: "France",
};

const unit = (id: string, name: string, changes: Partial<ArmyUnitResponse> = {}) =>
  ({
    id,
    armyId,
    unitId: null,
    factionId: null,
    nation: "France",
    name,
    type: "Boat",
    fightingFactor: 1,
    points: 0,
    ...changes,
  }) satisfies ArmyUnitResponse;

const at = (unitId: string, changes: Partial<UnitPosition> = {}): UnitPosition => ({
  unitId,
  armyId,
  turn: 0,
  status: "Completed",
  kind: "Hold",
  q: 0,
  r: 0,
  latitude: 50.7,
  longitude: 4.4,
  path: [],
  byUmpire: false,
  progress: null,
  forceMarch: false,
  livesOffTheLand: false,
  boats: [],
  carriedBy: null,
  ...changes,
});

const running: CampaignTurnsResponse = {
  stage: "Running",
  openTurn: 1,
  startProblems: [],
  turns: [
    {
      number: 1,
      openedAt: "2026-09-02T12:00:00Z",
      closedAt: null,
      submitted: 0,
      armies: 1,
      armyTurns: [],
    },
  ],
};

const draft: ArmyTurnDetails = {
  id: turnId,
  turn: 1,
  open: true,
  status: "Draft",
  submittedAt: null,
  completedAt: null,
  orders: [],
  history: [],
};

const edge = (side: "N" | "NE" | "SE", changes: { river?: boolean; waterway?: "Out" }) => ({
  q: 0,
  r: 0,
  side,
  road: "None" as const,
  river: changes.river ?? false,
  bridge: false,
  waterway: changes.waterway ?? ("None" as const),
  setByUmpire: true,
});

/**
 * The signed-in Player's army: the Imperial Guard (30 points: three boats of 14) at (0, 0), with
 * as many boats as given there, where these positions say, on this grid.
 */
function serve({
  boats = 3,
  positions = [at(guardId), ...boatIds.slice(0, boats).map((id) => at(id))],
  grid = { cells: [], edges: [] },
}: {
  boats?: number;
  positions?: UnitPosition[];
  grid?: CampaignGridResponse;
} = {}) {
  const sent: unknown[] = [];
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Hundred Days",
        description: null,
        umpire: null,
        myRole: "Player",
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
    http.get(`*/api/campaigns/${campaignId}/grid`, () => HttpResponse.json(grid)),
    http.get(`*/api/campaigns/${campaignId}/armies`, () => HttpResponse.json([army])),
    http.get(`*/api/campaigns/${campaignId}/units`, () =>
      HttpResponse.json([
        unit(guardId, "Imperial Guard", { type: "LineInfantry", fightingFactor: 6, points: 30 }),
        ...boatIds.slice(0, boats).map((id, i) => unit(id, `Boat ${String(i + 1)}`)),
      ]),
    ),
    http.get(`*/api/campaigns/${campaignId}/turns`, () => HttpResponse.json(running)),
    http.get(`*/api/campaigns/${campaignId}/positions`, () => HttpResponse.json(positions)),
    http.get(`*/api/armies/${armyId}/turns`, () => HttpResponse.json([draft])),
    http.put(`*/api/army-turns/${turnId}/orders/${guardId}`, async ({ request }) => {
      sent.push(await request.json());
      return HttpResponse.json(at(guardId, { turn: 1, status: "Draft" }));
    }),
  );
  return sent;
}

const openGuard = async () => {
  const user = userEvent.setup();
  await renderApp(`/campaigns/${campaignId}/map`);
  await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));
  return { user, drawer: within(await screen.findByRole("dialog")) };
};

/** The Guard on its three boats, embarked last turn. */
const aboard = [
  at(guardId, { kind: "Embark", boats: boatIds }),
  ...boatIds.map((id) => at(id, { carriedBy: guardId })),
];

describe("boats on the map", () => {
  it("embarks a unit on its army's free boats in its hex", async () => {
    const sent = serve();
    const { user, drawer } = await openGuard();

    expect(drawer.getByText(/It needs 3 boats; 3 free here/)).toBeInTheDocument();
    await user.click(drawer.getByRole("button", { name: "Embark" }));

    expect(await screen.findByText("Imperial Guard will embark.")).toBeInTheDocument();
    expect(sent).toEqual([{ kind: "Embark", path: null, livesOffTheLand: false }]);
  });

  it("won't embark without enough boats", async () => {
    serve({ boats: 2 });
    const { drawer } = await openGuard();

    expect(drawer.getByText(/It needs 3 boats; 2 free here/)).toBeInTheDocument();
    expect(drawer.getByRole("button", { name: "Embark" })).toBeDisabled();
  });

  it("lists a unit on boats without them, and lands it across a river side", async () => {
    const sent = serve({
      positions: aboard,
      grid: { cells: [], edges: [edge("N", { river: true })] },
    });
    const { user, drawer } = await openGuard();

    // Its boats go by its orders: they're not listed on their own.
    const panel = screen.getByRole("region", { name: "Turn 1" });
    expect(within(panel).queryByText("Boat 1")).not.toBeInTheDocument();
    expect(drawer.getByText(/On 3 boats/)).toBeInTheDocument();
    expect(drawer.queryByRole("switch", { name: /Living off the land/ })).not.toBeInTheDocument();
    await user.click(drawer.getByRole("button", { name: "Land" }));
    expect(screen.getByText(/Tap a shaded hex for where/)).toHaveTextContent("Imperial Guard");
    await user.click(screen.getByRole("button", { name: "Click the map" }));

    expect(await screen.findByText("Imperial Guard will land.")).toBeInTheDocument();
    expect(sent).toEqual([{ kind: "Disembark", path: [{ q: 0, r: -1 }], livesOffTheLand: false }]);
  });

  it("won't land where there's no river side to land across", async () => {
    const sent = serve({ positions: aboard });
    const { user, drawer } = await openGuard();

    await user.click(drawer.getByRole("button", { name: "Land" }));
    await user.click(screen.getByRole("button", { name: "Click the map" }));

    expect(await screen.findByText(/lands in its hex, across a river side/)).toBeInTheDocument();
    expect(sent).toEqual([]);
  });

  it("builds a boat in a town on a waterway", async () => {
    const sent = serve({
      boats: 0,
      grid: {
        cells: [
          {
            q: 0,
            r: 0,
            terrain: "Flat",
            forest: false,
            settlement: { ...noSettlement, size: "Town", name: "Wavre" },
            setByUmpire: true,
          },
        ],
        edges: [edge("SE", { waterway: "Out" })],
      },
    });
    const { user, drawer } = await openGuard();

    expect(drawer.queryByRole("button", { name: "Embark" })).not.toBeInTheDocument();
    await user.click(drawer.getByRole("button", { name: "Build a boat" }));

    expect(await screen.findByText("Imperial Guard will build a boat.")).toBeInTheDocument();
    expect(sent).toEqual([{ kind: "BuildBoat", path: null, livesOffTheLand: false }]);
  });
});
