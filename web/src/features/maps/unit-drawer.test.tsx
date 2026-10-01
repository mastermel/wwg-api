import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { ArmySummary } from "@/api/generated/model";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import type { PlacedUnit } from "@/features/maps/stacks";
import { UnitDrawer } from "@/features/maps/UnitDrawer";
import { server } from "@/test/server";

const army: ArmySummary = {
  id: "a",
  name: "Armée du Nord",
  commander: null,
  side: { id: "0192f5c1-0000-7000-8000-00000000f001", name: "Coalition" },
  color: "Blue",
  nation: "France",
};

const unit = (id: string, name: string, type: PlacedUnit["unit"]["type"]): PlacedUnit => ({
  unit: {
    id,
    armyId: "a",
    unitId: "l",
    factionId: "f",
    nation: "France",
    name,
    type,
    fightingFactor: 6,
    points: 30,
  },
  army,
  hex: { q: 0, r: 0 },
  latitude: 50.7,
  longitude: 4.4,
});

const stack = [
  unit("g", "Imperial Guard", "LineInfantry"),
  unit("r", "Reserve Artillery", "FootArtillery"),
];

function Harness({ units, marches = false }: { units: PlacedUnit[]; marches?: boolean }) {
  const [selected, setSelected] = useState<PlacedUnit | null>(null);
  return (
    <UnitDrawer
      units={units}
      selected={selected}
      onSelect={setSelected}
      onClose={() => undefined}
      actions={(chosen) => <button type="button">Order {chosen.unit.name}</button>}
      showsMarches={() => marches}
    />
  );
}

const renderDrawer = (units: PlacedUnit[], marches = false) =>
  render(
    <AppProviders queryClient={createQueryClient()}>
      <Harness units={units} marches={marches} />
    </AppProviders>,
  );

describe("the unit drawer", () => {
  it("lists a stack's units, then shows the one chosen, with what can be done", async () => {
    renderDrawer(stack);
    const dialog = within(await screen.findByRole("dialog", { name: "2 units here" }));

    await userEvent.click(
      dialog.getByRole("button", { name: "Reserve Artillery, Foot Artillery, Armée du Nord" }),
    );

    expect(await screen.findByRole("dialog", { name: "Reserve Artillery" })).toBeInTheDocument();
    expect(screen.getByText("Foot Artillery")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Order Reserve Artillery" })).toBeInTheDocument();
  });

  it("shows a single unit straight away", async () => {
    renderDrawer(stack.slice(0, 1));

    expect(await screen.findByRole("dialog", { name: "Imperial Guard" })).toBeInTheDocument();
    expect(screen.getByText("Armée du Nord")).toBeInTheDocument();
  });

  it("shows its points history, and its forced marches to whoever follows its moves", async () => {
    server.use(
      http.get("*/api/army-units/g/points", () =>
        HttpResponse.json([
          {
            turn: 4,
            change: -3,
            pointsAfter: 27,
            reason: "Attrition",
            note: "Forced march",
            byName: "Ada Tester",
            at: "2026-09-04T12:00:00Z",
          },
          {
            turn: 5,
            change: 2,
            pointsAfter: 29,
            reason: "Edited",
            note: null,
            byName: "Ada Tester",
            at: "2026-09-05T12:00:00Z",
          },
        ]),
      ),
      http.get("*/api/armies/a/marches", () =>
        HttpResponse.json([
          {
            unitId: "g",
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
    renderDrawer(stack.slice(0, 1), true);

    const history = await screen.findByRole("list", { name: "Points history" });
    expect(
      within(history)
        .getAllByRole("listitem")
        .map((item) => item.textContent),
    ).toEqual(["Turn 4: −3, to 27. Forced march.", "Turn 5: +2, to 29. Changed by Ada Tester."]);
    expect(
      await screen.findByText("Force marching: 2 turns to rest off (a Hold each)."),
    ).toBeInTheDocument();
  });

  it("keeps a unit's forced marches from whoever doesn't follow its moves", async () => {
    renderDrawer(stack.slice(0, 1));

    expect(await screen.findByRole("dialog", { name: "Imperial Guard" })).toBeInTheDocument();
    expect(screen.queryByText("Marches")).not.toBeInTheDocument();
  });
});
