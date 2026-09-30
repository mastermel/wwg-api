import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type {
  ArmyColor,
  ArmyResponse,
  ArmySummary,
  CampaignMemberResponse,
  CampaignResponse,
} from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const sideId = "0192f5c1-0000-7000-8000-00000000f001";
const armyId = "0192f5c1-0000-7000-8000-00000000a001";
const otherArmyId = "0192f5c1-0000-7000-8000-00000000a002";

const me: CampaignMemberResponse = {
  id: "0192f5c1-0000-7000-8000-00000000d001",
  userId: testUser.id,
  firstName: "Mel",
  lastName: "Green",
  role: "Player",
  joinedAt: "2026-09-01T12:00:00Z",
  army: { id: armyId, name: "First Corps", color: "Red", nation: "None" },
};

const arthur: CampaignMemberResponse = {
  id: "0192f5c1-0000-7000-8000-00000000d002",
  userId: "0192f5c1-0000-7000-8000-0000000000a2",
  firstName: "Arthur",
  lastName: "Wellesley",
  role: "Player",
  joinedAt: "2026-09-02T12:00:00Z",
  army: null,
};

const commander = (member: CampaignMemberResponse) => ({
  memberId: member.id,
  userId: member.userId,
  firstName: member.firstName,
  lastName: member.lastName,
});

/** An army's side, colour and nation: none, the given colour, a plain flag. */
const factionId = "0192f5c1-0000-7000-8000-0000000fac01";
const identity = (color: ArmyColor) => ({ side: null, color, nation: "None" as const });

const armies: ArmySummary[] = [
  { ...identity("Red"), id: armyId, name: "First Corps", commander: commander(me) },
  { ...identity("Blue"), id: otherArmyId, name: "Reserve", commander: null },
];

const firstCorps: ArmyResponse = {
  ...identity("Red"),
  id: armyId,
  campaignId,
  campaignName: "The Peninsular War",
  name: "First Corps",
  commander: commander(me),
  factions: [],
  units: [],
  createdAt: "2026-09-01T12:00:00Z",
  updatedAt: "2026-09-01T12:00:00Z",
};

function serveCampaign(myRole: CampaignResponse["myRole"], members = [me, arthur]) {
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Peninsular War",
        description: null,
        umpire: null,
        myRole,
        playerCount: 2,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      } satisfies CampaignResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/members`, () => HttpResponse.json(members)),
    http.get(`*/api/campaigns/${campaignId}/armies`, () => HttpResponse.json(armies)),
    http.get(`*/api/armies/${armyId}`, () => HttpResponse.json(firstCorps)),
  );
}

const armiesTable = async () =>
  within(await screen.findByRole("region", { name: "Armies" })).findByRole("table");

describe("armies on the campaign page", () => {
  it("shows a Player every army, and every one opens", async () => {
    serveCampaign("Player");
    await renderApp(`/campaigns/${campaignId}`);
    const table = await armiesTable();

    expect(within(table).getByRole("link", { name: "First Corps" })).toHaveAttribute(
      "href",
      `/campaigns/${campaignId}/armies/${armyId}`,
    );
    expect(within(table).getByRole("link", { name: "Reserve" })).toHaveAttribute(
      "href",
      `/campaigns/${campaignId}/armies/${otherArmyId}`,
    );
    // Reserve has no commander; neither army has a side.
    expect(within(table).getAllByText("Unassigned").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "New army" })).not.toBeInTheDocument();
  });

  it("shows each member's army", async () => {
    serveCampaign("Player");
    await renderApp(`/campaigns/${campaignId}`);

    const members = within(await screen.findByRole("region", { name: "Members" }));
    const row = (await members.findByText("Mel Green")).closest("tr");
    if (!row) throw new Error("No row");
    expect(within(row).getByText("First Corps")).toBeInTheDocument();
  });

  it("lets the Umpire add an army with a free Player as commander", async () => {
    serveCampaign("Umpire");
    const bodies: unknown[] = [];
    server.use(
      http.post(`*/api/campaigns/${campaignId}/armies`, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(
          { ...firstCorps, id: otherArmyId, name: "Second Corps", commander: commander(arthur) },
          { status: 201 },
        );
      }),
    );
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}`);

    await user.click(await screen.findByRole("button", { name: "New army" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByRole("textbox", { name: "Name" }), " Second Corps ");
    await user.click(within(dialog).getByRole("combobox", { name: "Commander" }));
    // Only Players without an army are offered.
    expect(within(dialog).queryByRole("option", { name: "Mel Green", hidden: true })).toBeNull();
    await user.click(
      await within(dialog).findByRole("option", { name: "Arthur Wellesley", hidden: true }),
    );
    await user.click(within(dialog).getByRole("button", { name: "Add army" }));

    expect(await screen.findByText("Added Second Corps.")).toBeInTheDocument();
    // Red and Blue are taken, so it suggests Green.
    expect(bodies).toEqual([
      {
        name: "Second Corps",
        commanderMemberId: arthur.id,
        sideId: null,
        color: "Green",
        nation: "None",
        factionIds: [],
      },
    ]);
  });

  it("shows the API's reason when an army can't be added", async () => {
    serveCampaign("Umpire");
    server.use(
      http.post(`*/api/campaigns/${campaignId}/armies`, () =>
        HttpResponse.json(
          { status: 409, title: "Already a commander", detail: "That Player already commands X." },
          { status: 409, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}`);

    await user.click(await screen.findByRole("button", { name: "New army" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByRole("textbox", { name: "Name" }), "Second Corps");
    await user.click(within(dialog).getByRole("button", { name: "Add army" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "That Player already commands X.",
    );
  });
});

describe("army page", () => {
  it("shows its commander their army, without the Umpire's controls", async () => {
    serveCampaign("Player");
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    expect(
      await screen.findByRole("heading", { level: 1, name: "First Corps" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Commanded by Mel Green (you)")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete army" })).not.toBeInTheDocument();
  });

  it("shows another Player the army and its side, read-only", async () => {
    serveCampaign("Player");
    server.use(
      http.get(`*/api/armies/${armyId}`, () =>
        HttpResponse.json({
          ...firstCorps,
          commander: commander(arthur),
          side: { id: sideId, name: "Coalition" },
          nation: "Britain",
        }),
      ),
    );
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    expect(
      await screen.findByRole("heading", { level: 1, name: "First Corps" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Coalition")).toBeInTheDocument();
    expect(screen.getByText("Commanded by Arthur Wellesley")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit army" })).not.toBeInTheDocument();
  });

  it("lets the Umpire change the army's side, colour, nation and factions", async () => {
    serveCampaign("Umpire");
    const bodies: unknown[] = [];
    server.use(
      http.get(`*/api/campaigns/${campaignId}/sides`, () =>
        HttpResponse.json([{ id: sideId, name: "Coalition", armyCount: 0 }]),
      ),
      http.get("*/api/factions", () =>
        HttpResponse.json([{ id: factionId, name: "Prussian", nation: "Prussia", unitCount: 3 }]),
      ),
      http.put(`*/api/armies/${armyId}`, async ({ request }) => {
        const body = (await request.json()) as object;
        bodies.push(body);
        return HttpResponse.json({ ...firstCorps, ...body });
      }),
    );
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    await user.click(await screen.findByRole("button", { name: "Edit army" }));
    const dialog = within(await screen.findByRole("dialog"));
    await user.click(dialog.getByRole("combobox", { name: "Side" }));
    await user.click(await dialog.findByRole("option", { name: "Coalition", hidden: true }));
    await user.click(dialog.getByRole("combobox", { name: "Colour" }));
    await user.click(await dialog.findByRole("option", { name: "Gold", hidden: true }));
    await user.click(dialog.getByRole("combobox", { name: "Nation" }));
    await user.click(await dialog.findByRole("option", { name: "Prussia", hidden: true }));
    await user.click(dialog.getByRole("combobox", { name: "Factions" }));
    await user.click(await dialog.findByRole("option", { name: "Prussian", hidden: true }));
    await user.click(dialog.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Saved First Corps.")).toBeInTheDocument();
    expect(bodies).toEqual([
      { name: "First Corps", sideId, color: "Gold", nation: "Prussia", factionIds: [factionId] },
    ]);
  });

  it("lets the Umpire change the commander", async () => {
    serveCampaign("Umpire");
    const bodies: unknown[] = [];
    server.use(
      http.put(`*/api/armies/${armyId}/commander`, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json({ ...firstCorps, commander: commander(arthur) });
      }),
    );
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    await user.click(await screen.findByRole("combobox", { name: "Change commander" }));
    await user.click(await screen.findByRole("option", { name: "Arthur Wellesley", hidden: true }));

    expect(
      await screen.findByText("Arthur Wellesley now commands First Corps."),
    ).toBeInTheDocument();
    expect(bodies).toEqual([{ memberId: arthur.id }]);
  });

  it("lets the Umpire remove the commander", async () => {
    serveCampaign("Umpire");
    let unassigned = false;
    server.use(
      http.delete(`*/api/armies/${armyId}/commander`, () => {
        unassigned = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    await userEvent.click(await screen.findByRole("button", { name: "Remove commander" }));

    expect(await screen.findByText("First Corps has no commander now.")).toBeInTheDocument();
    expect(unassigned).toBe(true);
  });

  it("lets the Umpire delete the army, back to the campaign", async () => {
    serveCampaign("Umpire");
    server.use(
      http.delete(`*/api/armies/${armyId}`, () => new HttpResponse(null, { status: 204 })),
    );
    const { router } = await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    await userEvent.click(await screen.findByRole("button", { name: "Delete army" }));
    await userEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete army" }),
    );

    expect(await screen.findByText("Deleted First Corps.")).toBeInTheDocument();
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/campaigns/${campaignId}`);
    });
  });

  it("has no detectable accessibility problems", async () => {
    serveCampaign("Umpire");
    const { container } = await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    await screen.findByRole("button", { name: "Delete army" });

    await expectNoAxeViolations(container);
  });
});
