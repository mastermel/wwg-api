import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import type {
  ArmySummary,
  ArmyTurnDetails,
  CampaignMapResponse,
  CampaignResponse,
  CampaignTurnsResponse,
} from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";

// MapLibre needs WebGL, which jsdom lacks: the map is a stand-in (e2e covers the real one).
vi.mock("@/features/maps/CampaignMap", () => ({
  CampaignMap: () => <div role="application" aria-label="Campaign map" />,
}));

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const nord: ArmySummary = {
  id: "0192f5c1-0000-7000-8000-00000000a001",
  name: "Armée du Nord",
  commander: {
    memberId: "0192f5c1-0000-7000-8000-00000000e001",
    userId: "0192f5c1-0000-7000-8000-00000000f001",
    firstName: "Bob",
    lastName: "Tester",
  },
  side: { id: "0192f5c1-0000-7000-8000-00000000f001", name: "Coalition" },
  color: "Blue",
  nation: "France",
};
const prussians: ArmySummary = {
  ...nord,
  id: "0192f5c1-0000-7000-8000-00000000a002",
  name: "Prussian I Corps",
  commander: null,
  color: "Red",
  nation: "Prussia",
};
const guardId = "0192f5c1-0000-7000-8000-00000000b001";

const armyTurn = (army: ArmySummary, changes: Partial<ArmyTurnDetails>): ArmyTurnDetails => ({
  id: army.id.replace("a00", "d00"),
  turn: 1,
  open: true,
  status: "Draft",
  submittedAt: null,
  completedAt: null,
  orders: [],
  history: [],
  ...changes,
});

const turns = (startProblems: string[]): CampaignTurnsResponse => ({
  stage: "Running",
  openTurn: 1,
  startProblems,
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
});

/** The Umpire's running campaign, with each army's open turn as given. */
function serveUmpire(nordTurn: ArmyTurnDetails, prussianTurn: ArmyTurnDetails, problems: string[]) {
  const requests: { url: string; body: unknown }[] = [];
  const record = async (request: Request) => {
    const text = await request.text();
    requests.push({
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
        myRole: "Umpire",
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
    http.get(`*/api/campaigns/${campaignId}/armies`, () => HttpResponse.json([nord, prussians])),
    http.get(`*/api/campaigns/${campaignId}/units`, () =>
      HttpResponse.json([
        {
          id: guardId,
          armyId: nord.id,
          name: "Imperial Guard",
          type: "LineInfantry",
          fightingFactor: 6,
          points: 30,
        },
      ]),
    ),
    http.get(`*/api/campaigns/${campaignId}/turns`, () => HttpResponse.json(turns(problems))),
    http.get(`*/api/campaigns/${campaignId}/positions`, () => HttpResponse.json([])),
    http.get(`*/api/campaigns/${campaignId}/attrition`, () => HttpResponse.json([])),
    http.get(`*/api/campaigns/${campaignId}/sightings/due`, () => HttpResponse.json([])),
    http.get(`*/api/armies/${nord.id}/turns`, () => HttpResponse.json([nordTurn])),
    http.get(`*/api/armies/${prussians.id}/turns`, () => HttpResponse.json([prussianTurn])),
    http.post("*/api/army-turns/:id/:action", async ({ request }) => {
      await record(request);
      return new HttpResponse(null, { status: 204 });
    }),
    http.post(`*/api/campaigns/${campaignId}/turns`, async ({ request }) => {
      await record(request);
      return HttpResponse.json(turns([]));
    }),
  );
  return requests;
}

const submitted = armyTurn(nord, {
  status: "Submitted",
  submittedAt: "2026-09-03T12:00:00Z",
  orders: [
    {
      unitId: guardId,
      armyId: nord.id,
      turn: 1,
      status: "Submitted",
      kind: "Move",
      latitude: 50.72,
      longitude: 4.4,
      byUmpire: false,
      progress: null,
      forceMarch: false,
      livesOffTheLand: false,
      boats: [],
      carriedBy: null,
      q: 0,
      r: 0,
      path: [],
    },
  ],
});
const approved = armyTurn(nord, {
  status: "Completed",
  submittedAt: "2026-09-03T12:00:00Z",
  completedAt: "2026-09-03T13:00:00Z",
});
const waiting = armyTurn(prussians, {});

const openMap = () => renderApp(`/campaigns/${campaignId}/map`);

describe("the Umpire's turn", () => {
  it("shows each army's turn, and what stops the next turn starting", async () => {
    serveUmpire(submitted, waiting, ["Prussian I Corps has no commander: submit its turn for it."]);

    await openMap();

    const panel = await screen.findByRole("region", { name: "Turn 1" });
    expect(await within(panel).findByText("Submitted")).toBeInTheDocument();
    expect(within(panel).getByText(/^1 move, 0 holds\. Submitted/)).toBeInTheDocument();
    expect(
      within(panel).getByText("No commander: give its orders on the map (0 so far)."),
    ).toBeInTheDocument();
    expect(
      within(panel).getByRole("button", { name: "Submit Prussian I Corps's turn for it" }),
    ).toBeInTheDocument();
    expect(
      within(panel).getByText("Prussian I Corps has no commander: submit its turn for it."),
    ).toBeInTheDocument();
    expect(within(panel).getByRole("button", { name: "Start turn 2" })).toBeDisabled();
    await expectNoAxeViolations(document.body);
  });

  it("approves a submitted turn", async () => {
    const requests = serveUmpire(submitted, waiting, []);
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Approve Armée du Nord's turn" }));

    expect(await screen.findByText("Approved Armée du Nord's turn 1.")).toBeInTheDocument();
    expect(requests).toEqual([{ url: `/api/army-turns/${submitted.id}/approve`, body: null }]);
  });

  it("sends a turn back with notes on it and on a unit", async () => {
    const requests = serveUmpire(submitted, waiting, []);
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Send back Armée du Nord's turn" }));
    const dialog = await screen.findByRole("dialog", { name: "Send back Armée du Nord's turn 1" });
    await user.type(within(dialog).getByRole("textbox", { name: "Note" }), " Too cautious. ");
    await user.type(
      within(dialog).getByRole("textbox", { name: "Imperial Guard" }),
      "Advance on the ridge.",
    );
    await user.click(within(dialog).getByRole("button", { name: "Send back" }));

    expect(await screen.findByText("Sent back Armée du Nord's turn 1.")).toBeInTheDocument();
    expect(requests).toEqual([
      {
        url: `/api/army-turns/${submitted.id}/send-back`,
        body: {
          note: "Too cautious.",
          unitNotes: [{ unitId: guardId, text: "Advance on the ridge." }],
        },
      },
    ]);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("reopens an approved turn, notes left out", async () => {
    const requests = serveUmpire(approved, waiting, []);
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Reopen Armée du Nord's turn" }));
    const dialog = await screen.findByRole("dialog", { name: "Reopen Armée du Nord's turn 1" });
    await user.click(within(dialog).getByRole("button", { name: "Reopen" }));

    expect(await screen.findByText("Reopened Armée du Nord's turn 1.")).toBeInTheDocument();
    expect(requests).toEqual([
      { url: `/api/army-turns/${approved.id}/revert`, body: { note: null, unitNotes: null } },
    ]);
  });

  it("starts the next turn after confirming", async () => {
    const requests = serveUmpire(approved, armyTurn(prussians, { status: "Completed" }), []);
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Start turn 2" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Start turn 2" }),
    );

    expect(await screen.findByText("Turn 2 has started.")).toBeInTheDocument();
    expect(requests).toEqual([
      { url: `/api/campaigns/${campaignId}/turns`, body: { attrition: [], sightings: [] } },
    ]);
  });

  it("asks the Umpire to confirm the attrition, each loss editable", async () => {
    const requests = serveUmpire(approved, armyTurn(prussians, { status: "Completed" }), []);
    server.use(
      http.get(`*/api/campaigns/${campaignId}/attrition`, () =>
        HttpResponse.json([
          {
            unitId: guardId,
            armyId: nord.id,
            name: "Imperial Guard",
            fightingFactor: 6,
            points: 30,
            forcedMarchTurns: 3,
            forcedMarchMultiplier: 2,
            unsuppliedTurns: 0,
            multiplier: 2,
            loss: 1,
          },
        ]),
      ),
    );
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Start turn 2" }));
    const dialog = await screen.findByRole("dialog");
    expect(
      await within(dialog).findByText(
        "Armée du Nord: FF 6, 30 points; its 3rd turn of forced march, double attrition.",
      ),
    ).toBeInTheDocument();
    const loss = within(dialog).getByRole("textbox", { name: "Points Imperial Guard loses" });
    expect(loss).toHaveValue("1");
    await user.clear(loss);
    expect(within(dialog).getByRole("button", { name: "Start turn 2" })).toBeDisabled();
    await user.type(loss, "2");
    await user.click(within(dialog).getByRole("button", { name: "Start turn 2" }));

    expect(await screen.findByText("Turn 2 has started.")).toBeInTheDocument();
    expect(requests).toEqual([
      {
        url: `/api/campaigns/${campaignId}/turns`,
        body: { attrition: [{ unitId: guardId, points: 2 }], sightings: [] },
      },
    ]);
  });

  it("submits an army's draft for it", async () => {
    const requests = serveUmpire(approved, waiting, []);
    const user = userEvent.setup();
    await openMap();

    await user.click(
      await screen.findByRole("button", { name: "Submit Prussian I Corps's turn for it" }),
    );

    expect(await screen.findByText("Submitted Prussian I Corps's turn 1.")).toBeInTheDocument();
    expect(requests).toEqual([{ url: `/api/army-turns/${waiting.id}/submit`, body: null }]);
  });
});

describe("the Umpire's couriers", () => {
  it("lists each on its way, flagged among the enemy, and stops one", async () => {
    serveUmpire(approved, waiting, []);
    const stopped: string[] = [];
    server.use(
      http.get(`*/api/campaigns/${campaignId}/couriers`, () =>
        HttpResponse.json([
          {
            reportId: "0192f5c1-0000-7000-8000-00000000f101",
            fromArmyId: nord.id,
            toArmyId: prussians.id,
            sentTurn: 1,
            q: 1,
            r: 0,
            latitude: 50.7,
            longitude: 4.5,
            arrivesNext: false,
            amongTheEnemy: true,
          },
        ]),
      ),
      http.post("*/api/reports/:id/stop", ({ params }) => {
        stopped.push(String(params.id));
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    await openMap();

    const couriers = await screen.findByRole("list", { name: "Couriers" });
    expect(couriers).toHaveTextContent(
      "Armée du Nord to Prussian I Corps, sent turn 1: Hex (1, 0).",
    );
    expect(couriers).toHaveTextContent("Among the enemy");
    await user.click(
      within(couriers).getByRole("button", {
        name: "Stop the courier from Armée du Nord to Prussian I Corps",
      }),
    );
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Stop it" }),
    );

    expect(
      await screen.findByText("Stopped the courier from Armée du Nord to Prussian I Corps."),
    ).toBeInTheDocument();
    expect(stopped).toEqual(["0192f5c1-0000-7000-8000-00000000f101"]);
  });
});

describe("sightings when starting the next turn", () => {
  it("prefills each army's sighting, for the Umpire to shape, and sends what they chose", async () => {
    const requests = serveUmpire(approved, armyTurn(prussians, { status: "Completed" }), []);
    server.use(
      http.get(`*/api/campaigns/${campaignId}/sightings/due`, () =>
        HttpResponse.json([
          {
            observingArmyId: nord.id,
            q: 1,
            r: 0,
            whereabouts: "1 hex south-east of Imperial Guard",
            screened: true,
            units: [
              {
                unitId: "0192f5c1-0000-7000-8000-00000000b009",
                armyId: prussians.id,
                name: "1st Brigade",
                type: "LightInfantry",
                points: 200,
                afloat: false,
              },
            ],
          },
        ]),
      ),
    );
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: "Start turn 2" }));
    const sighting = await within(await screen.findByRole("dialog")).findByRole("group", {
      name: "Armée du Nord sees Hex (1, 0)",
    });
    expect(sighting).toHaveTextContent("Possible screen");
    expect(sighting).toHaveTextContent("1st Brigade (Light Infantry, 200 points)");
    await user.click(within(sighting).getByRole("checkbox", { name: "The hex" }));
    await user.click(within(sighting).getByText("Exact"));
    await user.click(
      within(screen.getByRole("dialog")).getByRole("button", { name: "Start turn 2" }),
    );

    expect(await screen.findByText("Turn 2 has started.")).toBeInTheDocument();
    expect(requests.at(-1)?.body).toEqual({
      attrition: [],
      sightings: [
        {
          observingArmyId: nord.id,
          q: 1,
          r: 0,
          showsHex: false,
          showsArmies: true,
          showsTypes: true,
          showsAfloat: false,
          strength: "Exact",
          size: null,
        },
      ],
    });
  });
});

describe("contact and concentration in the Umpire's turn", () => {
  const french = {
    ...prussians,
    side: { id: "0192f5c1-0000-7000-8000-00000000f002", name: "French Empire" },
  };
  const lancersId = "0192f5c1-0000-7000-8000-00000000b002";
  const at = (unitId: string, armyId: string, q: number, r: number) => ({
    unitId,
    armyId,
    turn: 0,
    status: "Completed" as const,
    kind: "Move" as const,
    latitude: 50.7,
    longitude: 4.4,
    byUmpire: true,
    progress: null,
    forceMarch: false,
    livesOffTheLand: false,
    boats: [],
    carriedBy: null,
    q,
    r,
    path: [],
  });

  /** The guard (Coalition) at (0, 0), French lancers (220 points of cavalry) at (1, 0). */
  function serveContact(guardOrder: ArmyTurnDetails["orders"]) {
    serveUmpire(armyTurn(nord, { orders: guardOrder }), armyTurn(french, {}), []);
    server.use(
      http.get(`*/api/campaigns/${campaignId}/armies`, () => HttpResponse.json([nord, french])),
      http.get(`*/api/campaigns/${campaignId}/units`, () =>
        HttpResponse.json([
          {
            id: guardId,
            armyId: nord.id,
            name: "Imperial Guard",
            type: "LineInfantry",
            fightingFactor: 6,
            points: 30,
          },
          {
            id: lancersId,
            armyId: french.id,
            name: "Lancers",
            type: "HeavyCavalry",
            fightingFactor: 5,
            points: 220,
          },
        ]),
      ),
      http.get(`*/api/campaigns/${campaignId}/positions`, () =>
        HttpResponse.json([at(guardId, nord.id, 0, 0), at(lancersId, french.id, 1, 0)]),
      ),
      http.get(`*/api/campaigns/${campaignId}/concentration`, () =>
        HttpResponse.json({
          infantryTypes: ["LineInfantry"],
          cavalryTypes: ["HeavyCavalry"],
          infantryLimit: 200,
          cavalryLimit: 160,
        }),
      ),
    );
  }

  it("warns of a side over a limit where the units are", async () => {
    serveContact([]);

    await openMap();

    const warnings = await screen.findByRole("list", { name: "Contact and concentration" });
    expect(warnings).toHaveTextContent(
      "Hex (1, 0): French Empire has 220 points of cavalry, over 160.",
    );
    expect(warnings).not.toHaveTextContent("Contact:");
    await expectNoAxeViolations(document.body);
  });

  it("warns of contact where the orders as given take a unit", async () => {
    serveContact([{ ...at(guardId, nord.id, 1, 0), turn: 1, status: "Draft", byUmpire: false }]);

    await openMap();

    const warnings = await screen.findByRole("list", { name: "Contact and concentration" });
    expect(warnings).toHaveTextContent(
      "Hex (1, 0): Contact: Coalition and French Empire. French Empire has 220 points of cavalry, over 160.",
    );
    expect(
      screen.getByText("Where the units will be, by the orders as given."),
    ).toBeInTheDocument();
  });
});
