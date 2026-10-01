import { describe, expect, it } from "vitest";
import type { DepotResponse, UnitSupplyResponse } from "@/api/generated/model";
import { describeSupply } from "@/features/maps/supply";

const fleurus: DepotResponse = {
  id: "d",
  armyId: "a",
  kind: "Main",
  name: "Fleurus",
  q: 0,
  r: 0,
  latitude: 0,
  longitude: 0,
  cutOffTurns: 0,
};

const supply = (changes: Partial<UnitSupplyResponse>): UnitSupplyResponse => ({
  unitId: "u",
  armyId: "a",
  state: "Supplied",
  depotId: "d",
  unsuppliedTurns: 0,
  nextState: "Supplied",
  nextUnsuppliedTurns: 0,
  ...changes,
});

describe("describeSupply", () => {
  it("says where a supplied unit draws from", () => {
    expect(describeSupply(supply({}), [fleurus])).toBe("Supplied from Fleurus.");
  });

  it("counts the turns out of supply, and when attrition starts", () => {
    expect(
      describeSupply(
        supply({
          state: "Unsupplied",
          depotId: null,
          unsuppliedTurns: 2,
          nextState: "Unsupplied",
          nextUnsuppliedTurns: 3,
        }),
        [],
      ),
    ).toBe("Out of supply: 2 turns (attrition from the 7th); still after this turn's orders.");
    expect(
      describeSupply(
        supply({ state: "Unsupplied", depotId: null, unsuppliedTurns: 7, nextState: "Supplied" }),
        [],
      ),
    ).toBe(
      "Out of supply: 7 turns, losing points to attrition; after this turn's orders, supplied.",
    );
  });

  it("warns when this turn's orders leave a unit out of supply", () => {
    expect(
      describeSupply(supply({ nextState: "Unsupplied", nextUnsuppliedTurns: 1 }), [fleurus]),
    ).toBe(
      "Supplied from Fleurus; after this turn's orders, out of supply: 1 turn (attrition from the 7th).",
    );
  });

  it("doesn't count a turn not yet closed", () => {
    expect(
      describeSupply(
        supply({
          state: "Unsupplied",
          depotId: null,
          nextState: "Unsupplied",
          nextUnsuppliedTurns: 1,
        }),
        [],
      ),
    ).toBe("Out of supply; still after this turn's orders.");
  });

  it("reads exempt units, living off the land, and untracked armies", () => {
    expect(describeSupply(supply({ state: "Exempt", nextState: "Exempt" }), [])).toBe(
      "Doesn't need supply.",
    );
    expect(
      describeSupply(supply({ state: "LivingOffTheLand", nextState: "LivingOffTheLand" }), []),
    ).toBe("Living off the land.");
    expect(describeSupply(supply({ state: "Untracked", nextState: "Untracked" }), [])).toBe(
      "Not tracked: its army has no depots.",
    );
  });
});
