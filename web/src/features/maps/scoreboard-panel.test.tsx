import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import type { ArmySummary, ScoreboardResponse } from "@/api/generated/model";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { ScoreboardPanel } from "@/features/maps/ScoreboardPanel";

const nord: ArmySummary = {
  id: "a1",
  name: "Armée du Nord",
  commander: null,
  side: { id: "s1", name: "French Empire" },
  color: "Blue",
  nation: "France",
};
const prussians: ArmySummary = {
  ...nord,
  id: "a2",
  name: "Prussian I Corps",
  side: { id: "s2", name: "Coalition" },
};

const scoreboard: ScoreboardResponse = {
  sides: [
    { sideId: "s2", name: "Coalition", points: 10, armies: [{ armyId: "a2", points: 10 }] },
    { sideId: "s1", name: "French Empire", points: 25, armies: [{ armyId: "a1", points: 25 }] },
  ],
  settlements: [{ q: 0, r: 0, latitude: 0, longitude: 0, name: "Namur", value: 25, armyId: "a1" }],
  turns: [
    {
      turn: 0,
      sides: [
        { sideId: "s2", points: 10 },
        { sideId: "s1", points: 0 },
      ],
    },
    {
      turn: 1,
      sides: [
        { sideId: "s2", points: 10 },
        { sideId: "s1", points: 25 },
      ],
    },
  ],
  changes: [
    {
      turn: 0,
      q: 0,
      r: 0,
      name: "Namur",
      value: 25,
      fromArmyId: null,
      toArmyId: "a2",
      byUmpire: true,
    },
    {
      turn: 1,
      q: 0,
      r: 0,
      name: "Namur",
      value: 25,
      fromArmyId: "a2",
      toArmyId: "a1",
      byUmpire: false,
    },
  ],
};

const renderPanel = (data: ScoreboardResponse) =>
  render(
    <AppProviders queryClient={createQueryClient()}>
      <ScoreboardPanel scoreboard={data} armies={[nord, prussians]} />
    </AppProviders>,
  );

describe("the scoreboard", () => {
  it("gives each side's total, its armies' parts, and what the viewer's side holds", () => {
    renderPanel(scoreboard);

    const sides = screen.getByRole("table", { name: "Victory points by side" });
    expect(
      within(sides)
        .getAllByRole("row")
        .map((r) => r.textContent),
    ).toEqual([
      "CoalitionPrussian I Corps: 10 points10 points",
      "French EmpireArmée du Nord: 25 points25 points",
    ]);
    expect(screen.getByRole("list", { name: "Settlements held" })).toHaveTextContent(
      "Namur (25 points)",
    );
  });

  it("keeps the history: each turn's totals, and the changes of hands", async () => {
    const user = userEvent.setup();
    renderPanel(scoreboard);

    await user.click(screen.getByRole("button", { name: "History" }));

    expect(await screen.findByRole("list", { name: "Changes of hands" })).toHaveTextContent(
      "Turn 1: Namur (25 points) taken by Armée du Nord from Prussian I Corps.",
    );
    expect(screen.getByRole("list", { name: "Changes of hands" })).toHaveTextContent(
      "Setup: Namur (25 points) given to Prussian I Corps by the Umpire.",
    );
  });

  it("shows nothing until there's anything to score", () => {
    renderPanel({
      sides: scoreboard.sides.map((side) => ({ ...side, points: 0 })),
      settlements: [],
      turns: [],
      changes: [],
    });

    expect(screen.queryByRole("region", { name: "Victory points" })).not.toBeInTheDocument();
  });
});
