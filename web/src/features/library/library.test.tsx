import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { FactionResponse, FactionSummary, UnitResponse } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testAdmin, testUser } from "@/test/session";

const manager = { ...testUser, isManager: true };
const factionId = "0192f5c1-0000-7000-8000-0000000fac01";
const guard: UnitResponse = {
  id: "0192f5c1-0000-7000-8000-0000000u0001",
  factionId,
  name: "Imperial Guard",
  type: "LineInfantry",
  fightingFactor: 7,
  points: 40,
};

/** The library, with one faction whose units change as the test adds and deletes them. */
function serveLibrary(initial: UnitResponse[] = [guard]) {
  let units = initial;
  const calls: string[] = [];
  const faction = (): FactionResponse => ({
    id: factionId,
    name: "French",
    nation: "France",
    units,
  });
  server.use(
    http.get("*/api/factions", () =>
      HttpResponse.json([
        { id: factionId, name: "French", nation: "France", unitCount: units.length },
      ] satisfies FactionSummary[]),
    ),
    http.post("*/api/factions", async ({ request }) => {
      const body = (await request.json()) as { name: string; nation: FactionResponse["nation"] };
      calls.push(`create ${body.name} ${body.nation}`);
      return HttpResponse.json({ id: factionId, ...body, units: [] }, { status: 201 });
    }),
    http.get(`*/api/factions/${factionId}`, () => HttpResponse.json(faction())),
    http.post(`*/api/factions/${factionId}/units`, async ({ request }) => {
      const body = (await request.json()) as Omit<UnitResponse, "id" | "factionId">;
      const added = { ...body, id: "0192f5c1-0000-7000-8000-0000000u0002", factionId };
      calls.push(`add ${body.name}`);
      units = [...units, added];
      return HttpResponse.json(added, { status: 201 });
    }),
    http.delete("*/api/units/:id", ({ params }) => {
      calls.push(`delete ${String(params.id)}`);
      units = units.filter((u) => u.id !== params.id);
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return calls;
}

describe("the library", () => {
  it("shows every signed-in user the factions and their units, without editing", async () => {
    serveLibrary();
    const user = userEvent.setup();
    const { container } = await renderApp("/library");

    expect(await screen.findByRole("heading", { level: 1, name: "Library" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "New faction" })).not.toBeInTheDocument();
    await expectNoAxeViolations(container);
    await user.click(await screen.findByRole("link", { name: "French" }));

    expect(await screen.findByRole("heading", { level: 1, name: "French" })).toBeInTheDocument();
    expect(await screen.findByRole("cell", { name: /Imperial Guard/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add unit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit Imperial Guard" })).not.toBeInTheDocument();
    await expectNoAxeViolations(container);
  });

  it("is in everyone's navigation", async () => {
    serveLibrary();
    await renderApp("/library");

    expect(
      within(await screen.findByRole("navigation", { name: "Main" })).getByRole("link", {
        name: "Library",
      }),
    ).toHaveAttribute("aria-current", "page");
  });

  it("lets a Manager add a faction, and goes to it", async () => {
    const calls = serveLibrary();
    const user = userEvent.setup();
    await renderApp("/library", { user: manager });

    await user.click(await screen.findByRole("button", { name: "New faction" }));
    const dialog = within(await screen.findByRole("dialog", { name: "New faction" }));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), "  French ");
    await user.click(dialog.getByRole("combobox", { name: "Nation" }));
    await user.click(await screen.findByRole("option", { name: "France", hidden: true }));
    await user.click(dialog.getByRole("button", { name: "Add faction" }));

    expect(await screen.findByText("Added French.")).toBeInTheDocument();
    expect(await screen.findByRole("heading", { level: 1, name: "French" })).toBeInTheDocument();
    expect(calls).toEqual(["create French France"]);
  });

  it("lets an Admin add a unit to a faction", async () => {
    const calls = serveLibrary([]);
    const user = userEvent.setup();
    const { container } = await renderApp(`/library/${factionId}`, { user: testAdmin });

    expect(await screen.findByText("No units yet")).toBeInTheDocument();
    await expectNoAxeViolations(container);
    await user.click(screen.getByRole("button", { name: "Add unit" }));
    const dialog = within(await screen.findByRole("dialog", { name: "Add unit" }));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), "Old Guard");
    await user.click(dialog.getByRole("combobox", { name: "Type" }));
    await user.click(await screen.findByRole("option", { name: "Line Infantry", hidden: true }));
    await user.type(dialog.getByRole("textbox", { name: "Fighting Factor (FF)" }), "8");
    await user.clear(dialog.getByRole("textbox", { name: "Points" }));
    await user.type(dialog.getByRole("textbox", { name: "Points" }), "45");
    await user.click(dialog.getByRole("button", { name: "Add unit" }));

    expect(await screen.findByText("Added Old Guard.")).toBeInTheDocument();
    expect(await screen.findByRole("cell", { name: /Old Guard/ })).toBeInTheDocument();
    expect(calls).toEqual(["add Old Guard"]);
  });

  it("asks for the type and FF before sending", async () => {
    const calls = serveLibrary([]);
    const user = userEvent.setup();
    await renderApp(`/library/${factionId}`, { user: manager });

    await user.click(await screen.findByRole("button", { name: "Add unit" }));
    const dialog = within(await screen.findByRole("dialog"));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), "Guard");
    await user.click(dialog.getByRole("button", { name: "Add unit" }));

    expect(await dialog.findByText("Choose a type.")).toBeInTheDocument();
    expect(dialog.getByText("Enter an FF from 1 to 9.")).toBeInTheDocument();
    expect(calls).toEqual([]);
  });

  it("lets a Manager delete a unit, once confirmed, and only an empty faction", async () => {
    const calls = serveLibrary();
    const user = userEvent.setup();
    await renderApp(`/library/${factionId}`, { user: manager });

    expect(await screen.findByRole("button", { name: "Delete faction" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Delete Imperial Guard" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete unit" }),
    );

    expect(await screen.findByText("Deleted Imperial Guard.")).toBeInTheDocument();
    expect(await screen.findByText("No units yet")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete faction" })).toBeEnabled();
    expect(calls).toEqual([`delete ${guard.id}`]);
  });
});
