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
  faction: null,
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

const hold = {
  unitId,
  armyId,
  turn: 1,
  status: "Draft",
  kind: "Hold",
  latitude: 50.7,
  longitude: 4.4,
  byUmpire: false,
} as const;

/** A running campaign where the signed-in Player commands one army with one unit. */
function serveCommander(turn: ArmyTurnDetails) {
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
        },
        hexSize: 4828,
        movementLimits: [{ unitType: "LineInfantry", metres: 5000 }],
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
    http.get(`*/api/campaigns/${campaignId}/turns`, () => HttpResponse.json(running)),
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
    click.at = { longitude: 4.4, latitude: 50.72 };
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
        body: { kind: "Hold", latitude: null, longitude: null },
      },
    ]);
  });

  it("moves a unit: choose Move, tap inside its range, then confirm", async () => {
    const requests = serveCommander(draft());
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Move" }),
    );
    expect(screen.getByText(/Tap the map inside the circle/)).toHaveTextContent("Imperial Guard");
    await user.click(screen.getByRole("button", { name: "Click the map" }));
    expect(screen.getByText(/to here\?/)).toHaveTextContent("Move Imperial Guard 2.2 km to here?");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    expect(await screen.findByText("Imperial Guard will move.")).toBeInTheDocument();
    expect(requests).toEqual([
      {
        method: "PUT",
        url: `/api/army-turns/${turnId}/orders/${unitId}`,
        body: { kind: "Move", longitude: 4.4, latitude: 50.72 },
      },
    ]);
    expect(screen.queryByText(/to here\?/)).not.toBeInTheDocument();
  });

  it("won't pick a point further than the unit can move", async () => {
    const requests = serveCommander(draft());
    const user = userEvent.setup();
    click.at = { longitude: 4.4, latitude: 50.78 };
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Imperial Guard" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Move" }),
    );
    await user.click(screen.getByRole("button", { name: "Click the map" }));

    expect(
      await screen.findByText("That's further than Imperial Guard can move in a turn (5 km)."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Confirm" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByText(/Tap the map inside the circle/)).not.toBeInTheDocument();
    expect(requests).toEqual([]);
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
