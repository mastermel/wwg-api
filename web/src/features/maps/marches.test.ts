import { describe, expect, it } from "vitest";
import type { UnitMarchResponse } from "@/api/generated/model";
import {
  attritionInWords,
  canForceMarch,
  describeMarch,
  forceMarchBonus,
  moveCost,
} from "@/features/maps/marches";
import { ratesOf } from "@/features/maps/movement";

const rules = ratesOf(undefined);
const march = (changes: Partial<UnitMarchResponse>): UnitMarchResponse => ({
  unitId: "u",
  movesInRow: 0,
  forceMarchesInRow: 0,
  forcedMarchTurns: 0,
  moveCosts: 0,
  forceMarchCosts: 0,
  orderCosts: 0,
  ...changes,
});

describe("forceMarchBonus", () => {
  it("is a flat hex's worth for the unit's class", () => {
    // Line infantry move two flat hexes a turn; light cavalry four.
    expect(forceMarchBonus(rules, "LineInfantry")).toBe(0.5);
    expect(forceMarchBonus(rules, "LightCavalry")).toBe(0.25);
  });
});

describe("canForceMarch", () => {
  it("is by day only, for a class that moves over land", () => {
    expect(canForceMarch(rules, "LineInfantry", "Morning")).toBe(true);
    expect(canForceMarch(rules, "LineInfantry", "Night")).toBe(false);
    expect(canForceMarch(rules, "Boat", "Afternoon")).toBe(false);
  });
});

describe("attritionInWords", () => {
  it("reads 1, 2 and more", () => {
    expect([0, 1, 2, 4].map(attritionInWords)).toEqual([
      null,
      "normal attrition",
      "double attrition",
      "4× attrition",
    ]);
  });
});

describe("describeMarch", () => {
  it("says how far into a run, or what's left to rest off", () => {
    expect(describeMarch(march({}))).toBe("Rested.");
    expect(describeMarch(march({ movesInRow: 2 }))).toBe(
      "Moved 2 turns in a row: a third makes a forced march.",
    );
    expect(describeMarch(march({ forcedMarchTurns: 1 }))).toBe(
      "Force marching: 1 turn to rest off (a Hold each).",
    );
  });
});

describe("moveCost", () => {
  it("says what moving costs, and which turn of forced march it is", () => {
    const tired = march({ forcedMarchTurns: 2, moveCosts: 2, forceMarchCosts: 2 });

    expect(moveCost(tired, false)).toBe(
      "Moving costs double attrition: its 3rd turn of forced march.",
    );
    expect(moveCost(march({ forceMarchCosts: 0 }), true)).toBeNull();
    expect(moveCost(undefined, false)).toBeNull();
  });
});
