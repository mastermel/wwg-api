import { describe, expect, it } from "vitest";
import { hexGrid, hexKey } from "@/features/maps/hex-grid";
import { affordable, flatRate, pathTo, reach } from "@/features/maps/movement";

// The Waterloo grid: 31 hexes, from (0, -2) in the north to (0, 2) in the south.
const grid = hexGrid({ west: 4.2, south: 50.6, east: 4.6, north: 50.8 }, 4828);
const centre = { q: 0, r: 0 };

describe("reach", () => {
  it("goes as far as the class's flat rate", () => {
    const found = reach(grid, centre, "LineInfantry");

    expect(flatRate("LineInfantry")).toBe(2);
    // The start, its six neighbours and the twelve hexes around them: all in this grid.
    expect(found.size).toBe(19);
    expect(found.get(hexKey({ q: 2, r: -2 }))?.cost).toBe(1);
    expect(found.has(hexKey({ q: 3, r: -3 }))).toBe(false);
  });

  it("keeps inside the grid", () => {
    const found = reach(grid, { q: 0, r: -2 }, "LightCavalry");

    expect(found.has(hexKey({ q: 0, r: -3 }))).toBe(false);
  });

  it("goes anywhere in the grid without a budget, for the Umpire", () => {
    expect(reach(grid, centre, "SupplyTrain", Infinity).size).toBe(grid.hexes().length);
  });
});

describe("pathTo", () => {
  it("gives the steps to a hex, the start left out", () => {
    const found = reach(grid, centre, "LineInfantry");

    const path = pathTo(found, { q: 0, r: -2 });

    expect(path).toEqual([
      { q: 0, r: -1 },
      { q: 0, r: -2 },
    ]);
    expect(pathTo(found, { q: 3, r: -3 })).toBeNull();
  });
});

describe("affordable", () => {
  it("allows three thirds", () => {
    expect(affordable(1 / 3 + 1 / 3 + 1 / 3)).toBe(true);
    expect(affordable(1.34)).toBe(false);
  });
});
