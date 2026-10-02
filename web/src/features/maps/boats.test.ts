import { describe, expect, it } from "vitest";
import type { ArmySummary, ArmyUnitResponse, UnitPosition } from "@/api/generated/model";
import {
  aboardAfter,
  boatsNeeded,
  canBuildBoatsAt,
  canEmbark,
  freeBoatsFor,
  landingHexes,
  tiedBoats,
} from "@/features/maps/boats";
import type { PlacedUnit } from "@/features/maps/stacks";
import { indexTerrain, noSettlement } from "@/features/maps/terrain";

const army = (id: string): ArmySummary => ({
  id,
  name: id,
  commander: null,
  side: { id: "s", name: "Side" },
  color: "Blue",
  nation: "France",
});

const placed = (
  id: string,
  type: ArmyUnitResponse["type"],
  changes: Partial<PlacedUnit> = {},
): PlacedUnit => ({
  unit: {
    id,
    armyId: "a",
    unitId: null,
    factionId: null,
    nation: "France",
    name: id,
    type,
    fightingFactor: 1,
    points: 20,
  },
  army: army("a"),
  hex: { q: 0, r: 0 },
  latitude: 0,
  longitude: 0,
  ...changes,
});

const edge = (side: "N" | "NE" | "SE", river: boolean, waterway: "None" | "Out" = "None") => ({
  q: 0,
  r: 0,
  side,
  road: "None" as const,
  river,
  bridge: false,
  waterway,
  setByUmpire: true,
});

describe("boatsNeeded", () => {
  it("is a boat for each capacity's worth of points, or part, and at least one", () => {
    expect([0, 14, 15, 20, 45].map((points) => boatsNeeded(points, 14))).toEqual([1, 1, 2, 2, 4]);
  });
});

describe("canEmbark", () => {
  it("lets any land unit board, but not boats or supply trains", () => {
    expect(["LineInfantry", "HeavyCavalry", "SiegeArtillery"].every((t) => canEmbark(t as never)));
    expect(canEmbark("Boat")).toBe(false);
    expect(canEmbark("SupplyTrain")).toBe(false);
  });
});

describe("aboardAfter", () => {
  it("is the order's boats, but none after a landing", () => {
    const order = { kind: "Move", boats: ["b"] } as Pick<UnitPosition, "kind" | "boats">;
    expect(aboardAfter(order)).toEqual(["b"]);
    expect(aboardAfter({ ...order, kind: "Disembark" })).toEqual([]);
  });
});

describe("freeBoatsFor", () => {
  it("is the army's boats in the hex, by name, not tied to a unit nor boarded by another", () => {
    const guard = placed("guard", "LineInfantry");
    const units = [
      guard,
      placed("Boat C", "Boat"),
      placed("Boat A", "Boat"),
      placed("Boat far", "Boat", { hex: { q: 1, r: 0 } }),
      placed("Boat theirs", "Boat", { army: army("b") }),
      placed("Boat tied", "Boat"),
      placed("Boat boarded", "Boat"),
      placed("other", "LineInfantry", { boats: ["Boat tied"] }),
    ];
    const orders = [{ unitId: "x", kind: "Embark", boats: ["Boat boarded"] } as UnitPosition];

    expect(freeBoatsFor(guard, units, orders).map((b) => b.unit.id)).toEqual(["Boat A", "Boat C"]);
    expect([...tiedBoats(units)]).toEqual(["Boat tied"]);
  });
});

describe("landingHexes", () => {
  it("is the hex itself, and across a river side", () => {
    const terrain = indexTerrain({ cells: [], edges: [edge("N", true), edge("NE", false)] });

    expect(landingHexes({ q: 0, r: 0 }, terrain)).toEqual([
      { q: 0, r: 0 },
      { q: 0, r: -1 },
    ]);
  });

  it("from a lake, is any shore beside it, never the water", () => {
    const water = (q: number, r: number) => ({
      q,
      r,
      terrain: "Water" as const,
      forest: false,
      settlement: noSettlement,
      setByUmpire: true,
    });
    const terrain = indexTerrain({ cells: [water(0, 0), water(1, 0)], edges: [] });

    const shores = landingHexes({ q: 0, r: 0 }, terrain);
    expect(shores).toHaveLength(5);
    expect(shores).not.toContainEqual({ q: 0, r: 0 });
    expect(shores).not.toContainEqual({ q: 1, r: 0 });
  });
});

describe("canBuildBoatsAt", () => {
  const town = {
    q: 0,
    r: 0,
    terrain: "Flat" as const,
    forest: false,
    settlement: { ...noSettlement, size: "Town" as const },
    setByUmpire: true,
  };

  it("needs a settlement with a waterway along a side", () => {
    const hex = { q: 0, r: 0 };
    expect(
      canBuildBoatsAt(hex, indexTerrain({ cells: [town], edges: [edge("SE", false, "Out")] })),
    ).toBe(true);
    expect(canBuildBoatsAt(hex, indexTerrain({ cells: [town], edges: [edge("SE", true)] }))).toBe(
      false,
    );
    expect(
      canBuildBoatsAt(hex, indexTerrain({ cells: [], edges: [edge("SE", false, "Out")] })),
    ).toBe(false);
  });
});
