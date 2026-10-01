import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type {
  ArmySummary,
  ArmyTurnDetails,
  CampaignMapResponse,
  CampaignResponse,
  CampaignTurnsResponse,
  UnitPosition,
} from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

// Where a click on the stand-in map lands; each test can move it.
const click = vi.hoisted(() => ({ at: { longitude: 4.4, latitude: 50.72 } }));

// MapLibre needs WebGL, which jsdom lacks: the map is a stand-in whose button clicks `click.at`.
// Its children (markers, ghosts, the range) need a real map, so they aren't drawn: e2e covers them.
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
const unitId = "0192f5c1-0000-7000-8000-00000000b001";
const turnId = "0192f5c1-0000-7000-8000-00000000d001";

const army: ArmySummary = {
  id: armyId,
  name: "Armée du Nord",
  commander: {
    memberId: "0192f5c1-0000-7000-8000-00000000e001",
    userId: testUser.id,
    firstName: testUser.firstName,
    lastName: testUser.lastName,
  },
  side: { id: "0192f5c1-0000-7000-8000-00000000f001", name: "Coalition" },
  color: "Blue",
  nation: "France",
};

const running: CampaignTurnsResponse = {
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
};

const draft = (changes: Partial<ArmyTurnDetails> = {}): ArmyTurnDetails => ({
  id: turnId,
  turn: 1,
  open: true,
  status: "Draft",
  submittedAt: null,
  completedAt: null,
  orders: [],
  history: [],
  ...changes,
});

const hold: UnitPosition = {
  unitId,
  armyId,
  turn: 1,
  status: "Draft",
  kind: "Hold",
  latitude: 50.7,
  longitude: 4.4,
  byUmpire: false,
  progress: null,
  forceMarch: false,
  livesOffTheLand: false,
  q: 0,
  r: 0,
  path: [],
};

/** A running campaign where the signed-in Player commands one army with one unit. */
function serveCommander(turn: ArmyTurnDetails, turns: CampaignTurnsResponse = running) {
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
    http.get(`*/api/campaigns/${campaignId}/armies`, () => HttpResponse.json([army])),
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
    http.get(`*/api/campaigns/${campaignId}/turns`, () => HttpResponse.json(turns)),
    http.get(`*/api/campaigns/${campaignId}/positions`, () =>
      HttpResponse.json([{ ...hold, turn: 0, status: "Completed", kind: "Move" }]),
    ),
    http.get(`*/api/armies/${armyId}/turns`, () => HttpResponse.json([turn])),
    http.put(`*/api/army-turns/${turnId}/orders/${unitId}`, async ({ request }) => {
      await record(request);
      return HttpResponse.json(hold);
    }),
    http.delete(`*/api/army-turns/${turnId}/orders/${unitId}`, async ({ request }) => {
      await record(request);
      return new HttpResponse(null, { status: 204 });
    }),
    http.post(`*/api/army-turns/${turnId}/submit`, async ({ request }) => {
      await record(request);
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return requests;
}

const openMap = () => renderApp(`/campaigns/${campaignId}/map`);

describe("a commander's turn", () => {
  beforeEach(() => {
    // In hex (0, -1): the one north of the Guard's (0, 0), in the middle of the area.
    click.at = { longitude: 4.45, latitude: 50.71 };
  });

  it("lists their units' orders, and holds Submit until every unit has one", async () => {
    serveCommander(draft());

    await openMap();

    const panel = await screen.findByRole("region", { name: "Turn 1" });
    expect(within(panel).getByText("1 of 2 armies have submitted this turn.")).toBeInTheDocument();
    expect(within(panel).getByText("Draft")).toBeInTheDocument();
    expect(within(panel).getByText("No order yet")).toBeInTheDocument();
    expect(within(panel).getByRole("button", { name: "Submit turn 1" })).toBeDisabled();
    expect(within(panel).getByText(/1 to go/)).toBeInTheDocument();
    await expectNoAxeViolations(document.body);
  });

  it("says when the turn falls, and marks a move by night", async () => {
    const night: CampaignTurnsResponse = {
      ...running,
      turns: running.turns.map((t) =>
        t.number === 1 ? { ...t, part: "Night", date: "1815-06-17" } : t,
      ),
    };
    serveCommander(
      draft({
        orders: [
          {
            unitId: unitId,
            armyId,
            turn: 1,
            status: "Draft",
            kind: "Move",
            q: 0,
            r: -1,
            latitude: 50.7,
            longitude: 4.4,
            path: [{ q: 0, r: -1 }],
            byUmpire: false,
            progress: null,
            forceMarch: false,
            livesOffTheLand: false,
          },
        ],
      }),
      night,
    );

    await openMap();

    const panel = await screen.findByRole("region", { name: "Turn 1" });
    expect(
      within(panel).getByText("17 June 1815, Night. 1 of 2 armies have submitted this turn."),
    ).toBeInTheDocument();
    expect(within(panel).getByRole("status")).toHaveTextContent(/It's night/);
    expect(within(panel).getByText("Moves 1 hex, by night")).toBeInTheDocument();
  });

  it("orders a unit to hold from its drawer", async () => {
    const requests = serveCommander(draft());
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Hold" }),
    );

    expect(await screen.findByText("Imperial Guard will hold.")).toBeInTheDocument();
    expect(requests).toEqual([
      {
        method: "PUT",
        url: `/api/army-turns/${turnId}/orders/${unitId}`,
        body: { kind: "Hold", path: null, livesOffTheLand: false },
      },
    ]);
  });

  it("moves a unit: choose Move, tap a hex it can reach, then confirm", async () => {
    const requests = serveCommander(draft());
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Move" }),
    );
    expect(screen.getByText(/Tap a shaded hex/)).toHaveTextContent("Imperial Guard");
    await user.click(screen.getByRole("button", { name: "Click the map" }));
    expect(screen.getByText(/to here\?/)).toHaveTextContent("Move Imperial Guard 1 hex to here?");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    expect(await screen.findByText("Imperial Guard will move.")).toBeInTheDocument();
    expect(requests).toEqual([
      {
        method: "PUT",
        url: `/api/army-turns/${turnId}/orders/${unitId}`,
        body: { kind: "Move", path: [{ q: 0, r: -1 }], forceMarch: false, livesOffTheLand: false },
      },
    ]);
    expect(screen.queryByText(/to here\?/)).not.toBeInTheDocument();
  });

  it("won't pick a hex further than the unit can move", async () => {
    const requests = serveCommander(draft());
    const user = userEvent.setup();
    // In hex (-3, 1): three hexes away, and line infantry moves two.
    click.at = { longitude: 4.272, latitude: 50.6967 };
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Move" }),
    );
    await user.click(screen.getByRole("button", { name: "Click the map" }));

    expect(
      await screen.findByText(
        "Imperial Guard can't get there this turn: it's too far, or the way is closed.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Confirm" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByText(/Tap a shaded hex/)).not.toBeInTheDocument();
    expect(requests).toEqual([]);
  });

  it("force marches a unit a hex further, and says what moving costs", async () => {
    const requests = serveCommander(draft());
    server.use(
      http.get(`*/api/armies/${armyId}/marches`, () =>
        HttpResponse.json([
          {
            unitId,
            movesInRow: 0,
            forceMarchesInRow: 0,
            forcedMarchTurns: 2,
            moveCosts: 2,
            forceMarchCosts: 2,
            orderCosts: 0,
          },
        ]),
      ),
    );
    const user = userEvent.setup();
    // In hex (-3, 1): three hexes away; line infantry moves two, or three by force march.
    click.at = { longitude: 4.272, latitude: 50.6967 };
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Move" }),
    );
    expect(
      await screen.findByText(/Moving costs double attrition: its 3rd turn of forced march\./),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("switch", { name: "Force march (a hex further)" }));
    await user.click(screen.getByRole("button", { name: "Click the map" }));
    await user.click(await screen.findByRole("button", { name: "Confirm" }));

    expect(await screen.findByText("Imperial Guard will force march.")).toBeInTheDocument();
    expect(requests).toEqual([
      expect.objectContaining({
        body: expect.objectContaining({ kind: "Move", forceMarch: true }) as unknown,
      }),
    ]);
  });

  it("lets a French unit live off the land, keeping its order", async () => {
    const requests = serveCommander(draft());
    server.use(
      http.get(`*/api/campaigns/${campaignId}/supply-settings`, () =>
        HttpResponse.json({ reach: 1, exemptTypes: [], offTheLandNations: ["France"] }),
      ),
      http.get(`*/api/campaigns/${campaignId}/units`, () =>
        HttpResponse.json([
          {
            id: unitId,
            armyId,
            unitId: "l",
            factionId: "f",
            nation: "France",
            name: "Imperial Guard",
            type: "LineInfantry",
            fightingFactor: 6,
            points: 30,
          },
        ]),
      ),
    );
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("switch", {
        name: /Living off the land/,
      }),
    );

    expect(await screen.findByText("Imperial Guard will live off the land.")).toBeInTheDocument();
    expect(requests).toEqual([
      expect.objectContaining({
        body: { kind: "Hold", path: null, livesOffTheLand: true },
      }),
    ]);
  });

  it("offers living off the land only to units whose nation may", async () => {
    serveCommander(draft());
    server.use(
      http.get(`*/api/campaigns/${campaignId}/supply-settings`, () =>
        HttpResponse.json({ reach: 1, exemptTypes: [], offTheLandNations: ["Spain"] }),
      ),
    );
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(screen.queryByRole("switch", { name: /Living off the land/ })).not.toBeInTheDocument();
  });

  it("warns that the orders leave a unit out of supply, and says so in its drawer", async () => {
    serveCommander(draft());
    server.use(
      http.get(`*/api/campaigns/${campaignId}/supply`, () =>
        HttpResponse.json({
          units: [
            {
              unitId,
              armyId,
              state: "Unsupplied",
              depotId: null,
              unsuppliedTurns: 6,
              nextState: "Unsupplied",
              nextUnsuppliedTurns: 7,
            },
          ],
          depots: [],
        }),
      ),
    );
    const user = userEvent.setup();
    await openMap();

    const warnings = await screen.findByRole("list", { name: "Supply" });
    expect(warnings).toHaveTextContent(
      "Imperial Guard: out of supply after this turn (its 7th turn: attrition).",
    );
    await user.click(screen.getByRole("button", { name: "Imperial Guard" }));
    expect(
      await within(await screen.findByRole("dialog")).findByText(
        "Out of supply: 6 turns (attrition from the 7th); still after this turn's orders.",
      ),
    ).toBeInTheDocument();
  });

  it("takes an order back", async () => {
    const requests = serveCommander(draft({ orders: [hold] }));
    const user = userEvent.setup();
    await openMap();

    expect(await screen.findByText("Holds")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Undo Imperial Guard's order" }));

    expect(await screen.findByText("Took back Imperial Guard's order.")).toBeInTheDocument();
    expect(requests.map((r) => r.method)).toEqual(["DELETE"]);
  });

  it("submits the turn after confirming", async () => {
    const requests = serveCommander(draft({ orders: [hold] }));
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Submit turn 1" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Submit" }),
    );

    expect(await screen.findByText("Submitted Armée du Nord's turn 1.")).toBeInTheDocument();
    expect(requests).toEqual([
      { method: "POST", url: `/api/army-turns/${turnId}/submit`, body: null },
    ]);
  });

  it("shows the Umpire's notes on a turn sent back", async () => {
    serveCommander(
      draft({
        orders: [hold],
        history: [
          {
            kind: "SentBack",
            at: "2026-09-03T12:00:00Z",
            byName: "Uma Umpire",
            note: "Too cautious.",
            unitNotes: [{ unitId, text: "Advance on the ridge." }],
          },
        ],
      }),
    );

    await openMap();

    expect(await screen.findByRole("status", { name: "Sent back" })).toHaveTextContent(
      "Uma Umpire sent this turn back: Too cautious.",
    );
    expect(screen.getByText("Umpire: Advance on the ridge.")).toBeInTheDocument();
  });

  it("changes nothing once submitted", async () => {
    serveCommander(draft({ status: "Submitted", orders: [hold] }));

    await openMap();

    expect(await screen.findByText("Submitted: the Umpire reviews it next.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Undo/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Submit turn/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Imperial Guard" })).not.toBeInTheDocument();
  });

  it("marks an order the Umpire set", async () => {
    serveCommander(draft({ orders: [{ ...hold, byUmpire: true }] }));

    await openMap();

    expect(await screen.findByText("Holds · set by the Umpire")).toBeInTheDocument();
  });
});
