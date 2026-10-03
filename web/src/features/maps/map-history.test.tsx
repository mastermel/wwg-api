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
const guardId = "0192f5c1-0000-7000-8000-00000000b001";
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

const summary = (number: number, closedAt: string | null, submitted: number) => ({
  number,
  openedAt: "2026-09-01T12:00:00Z",
  closedAt,
  submitted,
  armies: 1,
  armyTurns: [
    {
      armyId: nord.id,
      status: closedAt ? ("Completed" as const) : ("Draft" as const),
      submittedAt: null,
      completedAt: closedAt,
    },
  ],
});

const turns: CampaignTurnsResponse = {
  stage: "Running",
  openTurn: 2,
  startProblems: ["Armée du Nord hasn't submitted yet."],
  turns: [
    summary(0, "2026-09-02T12:00:00Z", 1),
    summary(1, "2026-09-03T12:00:00Z", 1),
    summary(2, null, 0),
  ],
};

const armyTurn = (turn: number, changes: Partial<ArmyTurnDetails> = {}): ArmyTurnDetails => ({
  id: `0192f5c1-0000-7000-8000-00000000d00${String(turn)}`,
  turn,
  open: turn === 2,
  status: turn === 2 ? "Draft" : "Completed",
  submittedAt: null,
  completedAt: null,
  orders: [],
  history: [],
  ...changes,
});

/** The Umpire's campaign at turn 2; returns the positions requests' search strings. */
function serveHistory() {
  const positionRequests: string[] = [];
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
    http.get(`*/api/campaigns/${campaignId}/armies`, () => HttpResponse.json([nord])),
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
    http.get(`*/api/campaigns/${campaignId}/turns`, () => HttpResponse.json(turns)),
    http.get(`*/api/campaigns/${campaignId}/positions`, ({ request }) => {
      positionRequests.push(new URL(request.url).search);
      return HttpResponse.json([]);
    }),
    http.get(`*/api/armies/${nord.id}/turns`, () =>
      HttpResponse.json([
        armyTurn(2),
        armyTurn(1, {
          history: [
            {
              kind: "Submitted",
              at: "2026-09-02T14:00:00Z",
              byName: "Bob Tester",
              note: "Holding the bridge.",
              unitNotes: [],
            },
            {
              kind: "SentBack",
              at: "2026-09-02T15:00:00Z",
              byName: "Ada Tester",
              note: "Too cautious.",
              unitNotes: [{ unitId: guardId, text: "Advance on the ridge." }],
            },
          ],
        }),
        armyTurn(0),
      ]),
    ),
  );
  return positionRequests;
}

const openMap = () => renderApp(`/campaigns/${campaignId}/map`);

describe("the turn list", () => {
  it("lists the turns newest first, with their progress", async () => {
    serveHistory();

    await openMap();

    const list = await screen.findByRole("list", { name: "Turns" });
    const items = within(list).getAllByRole("button");
    expect(items.map((item) => item.textContent)).toEqual([
      "Turn 2 (open)0 of 1 submitted",
      "Turn 11 of 1 submitted",
      "Turn 0Setup",
    ]);
    expect(items[0]).toHaveAttribute("aria-pressed", "true");
  });

  it("shows a past turn: where the units were, and what happened to each army's turn", async () => {
    const positionRequests = serveHistory();
    const user = userEvent.setup();
    await openMap();

    await user.click(await screen.findByRole("button", { name: /^Turn 1:/ }));

    expect(
      await screen.findByText("Showing where the units were after turn 1."),
    ).toBeInTheDocument();
    expect(positionRequests.at(-1)).toBe("?turn=1");
    const history = screen.getByRole("list", { name: "What happened to Armée du Nord's turn" });
    expect(within(history).getByText(/^Submitted by Bob Tester/)).toBeInTheDocument();
    expect(
      within(history).getByText("Note to the Umpire: Holding the bridge."),
    ).toBeInTheDocument();
    expect(
      within(history).getByText(/^Sent back by Ada Tester, .*: Too cautious\.$/),
    ).toBeInTheDocument();
    expect(within(history).getByText("Imperial Guard: Advance on the ridge.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Start turn/ })).not.toBeInTheDocument();
    await expectNoAxeViolations(document.body);

    await user.click(screen.getByRole("button", { name: "Back to now" }));

    expect(screen.queryByText(/Showing where/)).not.toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Start turn 3" })).toBeInTheDocument();
  });

  it("steps through the turns with the arrow keys", async () => {
    const positionRequests = serveHistory();
    const user = userEvent.setup();
    await openMap();
    const newest = await screen.findByRole("button", { name: /^Turn 2 \(open\):/ });
    newest.focus();

    await user.keyboard("{ArrowDown}");
    expect(screen.getByRole("button", { name: /^Turn 1:/ })).toHaveFocus();
    await user.keyboard("{ArrowLeft}");

    const setup = screen.getByRole("button", { name: /^Turn 0:/ });
    expect(setup).toHaveFocus();
    expect(setup).toHaveAttribute("aria-pressed", "true");
    expect(
      await screen.findByText("Showing where the Umpire placed the units."),
    ).toBeInTheDocument();
    expect(positionRequests.at(-1)).toBe("?turn=0");
    await user.keyboard("{ArrowUp}{ArrowRight}");
    expect(screen.queryByText(/Showing where/)).not.toBeInTheDocument();
  });

  it("lets the Umpire pick out an army, and put it back", async () => {
    serveHistory();
    const user = userEvent.setup();
    await openMap();

    const army = await screen.findByRole("button", {
      name: "Armée du Nord: Bob Tester, 0 on the map",
    });
    expect(army).toHaveAttribute("aria-pressed", "false");
    await user.click(army);
    expect(army).toHaveAttribute("aria-pressed", "true");
    await user.click(army);
    expect(army).toHaveAttribute("aria-pressed", "false");
  });
});

describe("contact and concentration on a past turn", () => {
  it("warns the Umpire of a hex over a limit, from where the units ended up", async () => {
    serveHistory();
    server.use(
      // After turn 1 the guard was at (0, 0); now, nowhere on the map.
      http.get(`*/api/campaigns/${campaignId}/positions`, ({ request }) =>
        HttpResponse.json(
          new URL(request.url).searchParams.get("turn") === "1"
            ? [
                {
                  unitId: guardId,
                  armyId: nord.id,
                  turn: 1,
                  status: "Completed",
                  kind: "Hold",
                  latitude: 50.7,
                  longitude: 4.4,
                  byUmpire: false,
                  progress: null,
                  q: 0,
                  r: 0,
                  path: [],
                },
              ]
            : [],
        ),
      ),
      http.get(`*/api/campaigns/${campaignId}/concentration`, () =>
        HttpResponse.json({
          infantryTypes: ["LineInfantry"],
          cavalryTypes: [],
          infantryLimit: 20,
          cavalryLimit: 160,
        }),
      ),
    );
    const user = userEvent.setup();
    await openMap();
    expect(
      screen.queryByRole("list", { name: "Contact and concentration" }),
    ).not.toBeInTheDocument();

    await user.click(await screen.findByRole("button", { name: /^Turn 1:/ }));

    const warnings = await screen.findByRole("list", { name: "Contact and concentration" });
    expect(warnings).toHaveTextContent("Hex (0, 0): Coalition has 30 points of infantry, over 20.");
    expect(screen.getByText("Where the units ended up.")).toBeInTheDocument();
  });
});
