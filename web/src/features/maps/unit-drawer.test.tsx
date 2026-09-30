import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import type { ArmySummary } from "@/api/generated/model";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import type { PlacedUnit } from "@/features/maps/stacks";
import { UnitDrawer } from "@/features/maps/UnitDrawer";

const army: ArmySummary = {
  id: "a",
  name: "Armée du Nord",
  commander: null,
  faction: null,
  color: "Blue",
  nation: "France",
};

const unit = (id: string, name: string, type: PlacedUnit["unit"]["type"]): PlacedUnit => ({
  unit: { id, armyId: "a", name, type, fightingFactor: 6, points: 30 },
  army,
  latitude: 50.7,
  longitude: 4.4,
});

const stack = [
  unit("g", "Imperial Guard", "LineInfantry"),
  unit("r", "Reserve Artillery", "FootArtillery"),
];

function Harness({ units }: { units: PlacedUnit[] }) {
  const [selected, setSelected] = useState<PlacedUnit | null>(null);
  return (
    <UnitDrawer
      units={units}
      selected={selected}
      onSelect={setSelected}
      onClose={() => undefined}
      actions={(chosen) => <button type="button">Order {chosen.unit.name}</button>}
    />
  );
}

const renderDrawer = (units: PlacedUnit[]) =>
  render(
    <AppProviders queryClient={createQueryClient()}>
      <Harness units={units} />
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
});
