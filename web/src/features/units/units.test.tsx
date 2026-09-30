import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { ArmyResponse, CampaignResponse, UnitResponse } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const armyId = "0192f5c1-0000-7000-8000-00000000a001";

const unit = (
  id: string,
  name: string,
  type: UnitResponse["type"] = "LineInfantry",
  fightingFactor = 5,
  points = 20,
): UnitResponse => ({
  id: `0192f5c1-0000-7000-8000-00000000e00${id}`,
  armyId,
  name,
  type,
  fightingFactor,
  points,
});

/** Serves a campaign and its army, with units that change as the test adds and removes them. */
function serveArmy(myRole: CampaignResponse["myRole"], initial: UnitResponse[]) {
  let units = initial;
  const requests: { method: string; path: string; body: unknown }[] = [];
  const army = (): ArmyResponse => ({
    side: null,
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
    http.post(`*/api/armies/${armyId}/units`, async ({ request }) => {
      const body = (await request.json()) as Omit<UnitResponse, "id" | "armyId">;
      requests.push({ method: "POST", path: "units", body });
      const added: UnitResponse = { ...body, id: unit(String(units.length + 1), "").id, armyId };
      units = [...units, added];
      return HttpResponse.json(added, { status: 201 });
    }),
    http.put("*/api/units/:id", async ({ params, request }) => {
      const body = (await request.json()) as Omit<UnitResponse, "id" | "armyId">;
      requests.push({ method: "PUT", path: String(params.id), body });
      units = units.map((u) => (u.id === params.id ? { ...u, ...body } : u));
      return HttpResponse.json(units.find((u) => u.id === params.id));
    }),
    http.delete("*/api/units/:id", ({ params }) => {
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

  it("lets the Umpire add a unit", async () => {
    const requests = serveArmy("Umpire", []);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();
    expect(section.getByText("No units yet")).toBeInTheDocument();

    await user.click(section.getByRole("button", { name: "Add unit" }));
    const dialog = within(await screen.findByRole("dialog"));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), "  Light Division ");
    await user.click(dialog.getByRole("combobox", { name: "Type" }));
    await user.click(await dialog.findByRole("option", { name: "Light Infantry", hidden: true }));
    await user.type(dialog.getByRole("textbox", { name: "Fighting Factor (FF)" }), "6");
    await user.clear(dialog.getByRole("textbox", { name: "Points" }));
    await user.type(dialog.getByRole("textbox", { name: "Points" }), "35");
    await user.click(dialog.getByRole("button", { name: "Add unit" }));

    expect(await screen.findByText("Added Light Division.")).toBeInTheDocument();
    expect(requests).toEqual([
      {
        method: "POST",
        path: "units",
        body: { name: "Light Division", type: "LightInfantry", fightingFactor: 6, points: 35 },
      },
    ]);
    expect(await section.findByText("Light Division")).toBeInTheDocument();
  });

  it("asks for the type and FF before sending", async () => {
    const requests = serveArmy("Umpire", []);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    await user.click((await unitsSection()).getByRole("button", { name: "Add unit" }));
    const dialog = within(await screen.findByRole("dialog"));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), "Guard");
    await user.click(dialog.getByRole("button", { name: "Add unit" }));

    expect(await dialog.findByText("Choose a type.")).toBeInTheDocument();
    expect(dialog.getByText("Enter an FF from 1 to 9.")).toBeInTheDocument();
    expect(requests).toEqual([]);
  });

  it("won't take an FF over 9", async () => {
    serveArmy("Umpire", []);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);

    await user.click((await unitsSection()).getByRole("button", { name: "Add unit" }));
    const dialog = within(await screen.findByRole("dialog"));
    const ff = dialog.getByRole("textbox", { name: "Fighting Factor (FF)" });
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
  it("lets the Umpire delete a unit after confirming", async () => {
    const requests = serveArmy("Umpire", [unit("1", "1st Division")]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();

    await user.click(section.getByRole("button", { name: "Delete 1st Division" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete unit" }),
    );

    expect(await screen.findByText("Deleted 1st Division.")).toBeInTheDocument();
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
