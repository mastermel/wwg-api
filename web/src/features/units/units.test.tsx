import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type {
  ArmyResponse,
  ArmyUnitResponse,
  CampaignResponse,
  FactionResponse,
  UnitResponse,
} from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const armyId = "0192f5c1-0000-7000-8000-00000000a001";
const factionId = "0192f5c1-0000-7000-8000-0000000fac01";

/** The French faction's units in the library; "1st Division" is the army's first unit's. */
const library: UnitResponse[] = [
  {
    id: "0192f5c1-0000-7000-8000-00000000b001",
    factionId,
    name: "1st Division",
    type: "LineInfantry",
    fightingFactor: 5,
    points: 20,
  },
  {
    id: "0192f5c1-0000-7000-8000-00000000b002",
    factionId,
    name: "Light Division",
    type: "LightInfantry",
    fightingFactor: 6,
    points: 35,
  },
  {
    id: "0192f5c1-0000-7000-8000-00000000b003",
    factionId,
    name: "Hussars",
    type: "LightCavalry",
    fightingFactor: 4,
    points: 15,
  },
];

const unit = (
  id: string,
  name: string,
  type: ArmyUnitResponse["type"] = "LineInfantry",
  fightingFactor = 5,
  points = 20,
): ArmyUnitResponse => ({
  id: `0192f5c1-0000-7000-8000-00000000e00${id}`,
  armyId,
  unitId: `0192f5c1-0000-7000-8000-00000000b00${id}`,
  factionId,
  nation: "France",
  name,
  type,
  fightingFactor,
  points,
});

/** Serves a campaign and its army, with units that change as the test adds and removes them. */
function serveArmy(
  myRole: CampaignResponse["myRole"],
  initial: ArmyUnitResponse[],
  factions: ArmyResponse["factions"] = [{ id: factionId, name: "French", nation: "France" }],
) {
  let units = initial;
  const requests: { method: string; path: string; body: unknown }[] = [];
  const army = (): ArmyResponse => ({
    side: { id: "0192f5c1-0000-7000-8000-00000000f001", name: "Coalition" },
    color: "Red",
    nation: "None",
    id: armyId,
    campaignId,
    campaignName: "The Peninsular War",
    name: "First Corps",
    commander: {
      memberId: "0192f5c1-0000-7000-8000-00000000d001",
      userId: testUser.id,
      firstName: "Mel",
      lastName: "Green",
    },
    factions,
    units,
    createdAt: "2026-09-01T12:00:00Z",
    updatedAt: "2026-09-01T12:00:00Z",
  });
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Peninsular War",
        description: null,
        umpire: null,
        myRole,
        playerCount: 1,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      } satisfies CampaignResponse),
    ),
    http.get(`*/api/armies/${armyId}`, () => HttpResponse.json(army())),
    http.get(`*/api/factions/${factionId}`, () =>
      HttpResponse.json({
        id: factionId,
        name: "French",
        nation: "France",
        units: library,
      } satisfies FactionResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/units`, () => HttpResponse.json(units)),
    http.post(`*/api/armies/${armyId}/units`, async ({ request }) => {
      const body = (await request.json()) as { unitIds: string[] };
      requests.push({ method: "POST", path: "units", body });
      const added = library
        .filter((u) => body.unitIds.includes(u.id))
        .map((u, i): ArmyUnitResponse => ({
          ...u,
          nation: "France",
          id: unit(String(units.length + i + 1), "").id,
          armyId,
          unitId: u.id,
        }));
      units = [...units, ...added];
      return HttpResponse.json(added);
    }),
    http.put("*/api/army-units/:id", async ({ params, request }) => {
      const body = (await request.json()) as Omit<ArmyUnitResponse, "id" | "armyId">;
      requests.push({ method: "PUT", path: String(params.id), body });
      units = units.map((u) => (u.id === params.id ? { ...u, ...body } : u));
      return HttpResponse.json(units.find((u) => u.id === params.id));
    }),
    http.delete("*/api/army-units/:id", ({ params }) => {
      requests.push({ method: "DELETE", path: String(params.id), body: null });
      units = units.filter((u) => u.id !== params.id);
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return requests;
}

const unitsSection = async () => within(await screen.findByRole("region", { name: "Units" }));

/** Each row's cell texts, in the Units section. */
const rows = () =>
  within(screen.getByRole("region", { name: "Units" }))
    .getAllByRole("row")
    .map((row) =>
      within(row)
        .queryAllByRole("cell")
        .map((cell) => cell.textContent),
    );

describe("units", () => {
  it("shows the commander their units with type, FF and points, read-only", async () => {
    serveArmy("Player", [
      unit("1", "1st Division", "LineInfantry", 5, 20),
      unit("2", "Hussars", "LightCavalry", 4, 15),
    ]);
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();

    expect(await section.findByText("Hussars")).toBeInTheDocument();
    // Cells: name (with the type under it on phones), type, FF, points.
    expect(rows().filter((cells) => cells.length > 0)).toEqual([
      ["1st DivisionLine Infantry", "Line Infantry", "5", "20"],
      ["HussarsLight Cavalry", "Light Cavalry", "4", "15"],
      // The footer: "2 units" is its row header; the points total is under Points.
      ["", "", "35"],
    ]);
    expect(section.getByRole("columnheader", { name: "FF" })).toBeInTheDocument();
    expect(section.getByRole("rowheader", { name: "2 units" })).toBeInTheDocument();
    expect(section.queryByRole("button")).not.toBeInTheDocument();
  });

  it("lets the Umpire add units from the army's factions, not those already in", async () => {
    const requests = serveArmy("Umpire", [unit("1", "1st Division")]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    expect(await screen.findByText("Units from French")).toBeInTheDocument();

    await user.click((await unitsSection()).getByRole("button", { name: "Add units" }));
    const dialog = within(await screen.findByRole("dialog", { name: "Add units" }));
    expect(await dialog.findByRole("group", { name: "French" })).toBeInTheDocument();
    expect(dialog.getByRole("checkbox", { name: "1st Division" })).toBeDisabled();
    expect(dialog.getByText("In First Corps")).toBeInTheDocument();
    expect(dialog.getByRole("button", { name: "Add units" })).toBeDisabled();
    await user.click(dialog.getByRole("checkbox", { name: "Light Division" }));
    await user.click(dialog.getByRole("checkbox", { name: "Hussars" }));
    await user.click(dialog.getByRole("button", { name: "Add 2 units" }));

    expect(await screen.findByText("Added 2 units.")).toBeInTheDocument();
    expect(requests).toEqual([
      { method: "POST", path: "units", body: { unitIds: [library[1]?.id, library[2]?.id] } },
    ]);
    expect(await (await unitsSection()).findByText("Light Division")).toBeInTheDocument();
  });

  it("asks the Umpire to choose the army's factions first", async () => {
    serveArmy("Umpire", [], []);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    expect(await screen.findByText("No factions yet")).toBeInTheDocument();

    await user.click((await unitsSection()).getByRole("button", { name: "Add units" }));

    expect(
      await within(await screen.findByRole("dialog")).findByText(
        /choose them with Edit army first/,
      ),
    ).toBeInTheDocument();
  });

  it("won't take an FF over 9", async () => {
    serveArmy("Umpire", [unit("1", "1st Division")]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    await user.click((await unitsSection()).getByRole("button", { name: "Edit 1st Division" }));
    const dialog = within(await screen.findByRole("dialog"));
    const ff = dialog.getByRole("textbox", { name: "Fighting Factor (FF)" });
    await user.clear(ff);
    // The "2" would make 12: it isn't taken.
    await user.type(ff, "12");

    expect(ff).toHaveValue("1");
  });

  it("lets the Umpire edit a unit", async () => {
    const requests = serveArmy("Umpire", [unit("1", "1st Division", "LineInfantry", 5, 20)]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();

    await user.click(section.getByRole("button", { name: "Edit 1st Division" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByRole("textbox", { name: "Name" })).toHaveValue("1st Division");
    expect(dialog.getByRole("combobox", { name: "Type" })).toHaveValue("Line Infantry");
    const ff = dialog.getByRole("textbox", { name: "Fighting Factor (FF)" });
    await user.clear(ff);
    await user.type(ff, "7");
    await user.click(dialog.getByRole("button", { name: "Save" }));

    await waitFor(() => {
      expect(requests).toEqual([
        {
          method: "PUT",
          path: unit("1", "").id,
          body: { name: "1st Division", type: "LineInfantry", fightingFactor: 7, points: 20 },
        },
      ]);
    });
    await waitFor(() => {
      expect(rows()[1]).toEqual(["1st DivisionLine Infantry", "Line Infantry", "7", "20", ""]);
    });
    expect(await screen.findByText("Saved 1st Division.")).toBeInTheDocument();
  });

  it("lets the Umpire remove a unit after confirming", async () => {
    const requests = serveArmy("Umpire", [unit("1", "1st Division")]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();

    await user.click(section.getByRole("button", { name: "Remove 1st Division" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Remove unit" }),
    );

    expect(await screen.findByText("Removed 1st Division.")).toBeInTheDocument();
    expect(await section.findByText("No units yet")).toBeInTheDocument();
    expect(requests.map((r) => r.method)).toEqual(["DELETE"]);
  });

  it("has no detectable accessibility problems", async () => {
    serveArmy("Umpire", [unit("1", "1st Division")]);
    const { container } = await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    await (await unitsSection()).findByText("1st Division");

    await expectNoAxeViolations(container);
  });
});
