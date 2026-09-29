import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ArmyColor, Nation } from "@/api/generated/model";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import { armyColorVariables, armyColors } from "@/features/armies/identity/army-colors";
import { nationLabel, nationOptions } from "@/features/armies/identity/nations";
import { expectNoAxeViolations } from "@/test/render";

function renderBadges() {
  return render(
    <AppProviders queryClient={createQueryClient()}>
      <ul>
        {Object.values(Nation).map((nation, i) => (
          <li key={nation}>
            <ArmyBadge
              army={{
                name: `${nation} army`,
                nation,
                color: Object.values(ArmyColor)[i % 8] ?? "Red",
              }}
            />
          </li>
        ))}
      </ul>
    </AppProviders>,
  );
}

describe("army identity", () => {
  it("draws every nation's flag beside the army's name", () => {
    const { container } = renderBadges();

    for (const nation of Object.values(Nation)) {
      expect(screen.getByText(`${nation} army`)).toBeInTheDocument();
    }
    // Decorative: the name says which army it is.
    expect(container.querySelectorAll('svg[aria-hidden="true"]')).toHaveLength(
      Object.values(Nation).length,
    );
  });

  it("has no detectable accessibility problems", async () => {
    const { container } = renderBadges();

    await expectNoAxeViolations(container);
  });

  it("names every nation the API has, and offers them all, No nation first", () => {
    expect(nationOptions.map((o) => o.value).sort()).toEqual([...Object.values(Nation)].sort());
    expect(nationOptions[0]).toEqual({ value: "None", label: "No nation" });
    expect(nationLabel("Wurttemberg")).toBe("Württemberg");
  });

  it("has a light and a dark shade of every colour the API has", () => {
    expect(Object.keys(armyColors).sort()).toEqual([...Object.values(ArmyColor)].sort());
    expect(armyColorVariables("dark")["--army-sky"]).toBe("#56b4e9");
  });
});
