import { describe, expect, it } from "vitest";
import type { ArmySummary, ArmyUnitResponse } from "@/api/generated/model";
import { describeUnit, stackUnits, type PlacedUnit } from "@/features/maps/stacks";

const army: ArmySummary = {
  id: "a",
  name: "Armée du Nord",
  commander: null,
  side: null,
  color: "Blue",
  nation: "France",
};

const placed = (id: string, longitude: number, latitude = 0): PlacedUnit => ({
  unit: {
    id,
    armyId: "a",
    name: id,
    type: "LineInfantry",
    fightingFactor: 5,
    points: 20,
  } satisfies ArmyUnitResponse,
  army,
  hex: { q: 0, r: 0 },
  latitude,
  longitude,
});

// One degree of longitude is ten pixels.
const project = ([lng, lat]: [number, number]) => ({ x: lng * 10, y: lat * 10 });

describe("stacking units", () => {
  it("stacks units whose markers would overlap, at the first one's position", () => {
    const stacks = stackUnits([placed("a", 0), placed("b", 1), placed("c", 10)], project);

    expect(stacks.map((s) => s.units.map((u) => u.unit.id))).toEqual([["a", "b"], ["c"]]);
    expect(stacks[0]).toMatchObject({ key: "a", longitude: 0 });
  });

  it("keeps apart units further than the radius", () => {
    expect(stackUnits([placed("a", 0), placed("b", 3)], project, 26)).toHaveLength(2);
  });

  it("describes a unit for a screen reader", () => {
    expect(describeUnit(placed("Imperial Guard", 0))).toBe(
      "Imperial Guard, Line Infantry, Armée du Nord",
    );
  });
});
