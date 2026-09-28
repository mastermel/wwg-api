import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { ArmyResponse, CampaignResponse, UnitResponse } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const armyId = "0192f5c1-0000-7000-8000-00000000a001";

const unit = (id: string, name: string): UnitResponse => ({
  id: `0192f5c1-0000-7000-8000-00000000e00${id}`,
  armyId,
  name,
});

/** Serves a campaign and its army, with units that change as the test adds and removes them. */
function serveArmy(myRole: CampaignResponse["myRole"], initial: UnitResponse[]) {
  let units = initial;
  const requests: { method: string; path: string; body: unknown }[] = [];
  const army = (): ArmyResponse => ({
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
      const body = (await request.json()) as { name: string };
      requests.push({ method: "POST", path: "units", body });
      const added = unit(String(units.length + 1), body.name);
      units = [...units, added];
      return HttpResponse.json(added, { status: 201 });
    }),
    http.put("*/api/units/:id", async ({ params, request }) => {
      const body = (await request.json()) as { name: string };
      requests.push({ method: "PUT", path: String(params.id), body });
      units = units.map((u) => (u.id === params.id ? { ...u, name: body.name } : u));
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

describe("units", () => {
  it("shows the commander their units, read-only", async () => {
    serveArmy("Player", [unit("1", "1st Division"), unit("2", "Light Division")]);
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();

    expect(section.getAllByRole("listitem").map((item) => item.textContent)).toEqual([
      "1st Division",
      "Light Division",
    ]);
    expect(section.queryByRole("button")).not.toBeInTheDocument();
    expect(section.queryByRole("textbox", { name: "New unit" })).not.toBeInTheDocument();
  });

  it("lets the Umpire add a unit", async () => {
    const requests = serveArmy("Umpire", []);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();
    expect(section.getByText("No units yet.")).toBeInTheDocument();

    await user.type(section.getByRole("textbox", { name: "New unit" }), "  Light Division ");
    await user.click(section.getByRole("button", { name: "Add unit" }));

    expect(await section.findByText("Light Division")).toBeInTheDocument();
    expect(requests).toEqual([{ method: "POST", path: "units", body: { name: "Light Division" } }]);
    expect(section.getByRole("textbox", { name: "New unit" })).toHaveValue("");
  });

  it("checks the new unit's name before sending", async () => {
    const requests = serveArmy("Umpire", []);
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();

    await userEvent.click(section.getByRole("button", { name: "Add unit" }));

    expect(
      await section.findByText(/./, { selector: ".mantine-InputWrapper-error" }),
    ).toBeVisible();
    expect(requests).toEqual([]);
  });

  it("lets the Umpire rename a unit", async () => {
    const requests = serveArmy("Umpire", [unit("1", "1st Division")]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    const section = await unitsSection();

    await user.click(section.getByRole("button", { name: "Rename 1st Division" }));
    const dialog = within(await screen.findByRole("dialog"));
    await user.clear(dialog.getByRole("textbox", { name: "Name" }));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), "Guards Division");
    await user.click(dialog.getByRole("button", { name: "Save" }));

    expect(await section.findByText("Guards Division")).toBeInTheDocument();
    expect(requests).toEqual([
      { method: "PUT", path: unit("1", "").id, body: { name: "Guards Division" } },
    ]);
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
    expect(await section.findByText("No units yet.")).toBeInTheDocument();
    expect(requests.map((r) => r.method)).toEqual(["DELETE"]);
  });

  it("has no detectable accessibility problems", async () => {
    serveArmy("Umpire", [unit("1", "1st Division")]);
    const { container } = await renderApp(`/campaigns/${campaignId}/armies/${armyId}`);
    await (await unitsSection()).findByText("1st Division");

    await expectNoAxeViolations(container);
  });
});
