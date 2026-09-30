import { describe, expect, it } from "vitest";
import figures from "../../../../testdata/movement.json";
import type { CampaignGridResponse, HexEdgeResponse, UnitType } from "@/api/generated/model";
import { hexGrid, hexKey } from "@/features/maps/hex-grid";
import {
  affordable,
  budgetFor,
  classOf,
  pathTo,
  ratesOf,
  reach,
  rulesTable,
  shareOfTheWay,
  stepCost,
} from "@/features/maps/movement";
import { indexTerrain, noSettlement } from "@/features/maps/terrain";

// The Waterloo grid: 31 hexes, from (0, -2) in the north to (0, 2) in the south.
const grid = hexGrid({ west: 4.2, south: 50.6, east: 4.6, north: 50.8 }, 4828);
const centre = { q: 0, r: 0 };
const rules = ratesOf(undefined);
const open = indexTerrain(undefined);

const edge = (
  q: number,
  r: number,
  side: HexEdgeResponse["side"],
  changes: Partial<HexEdgeResponse>,
): HexEdgeResponse => ({
  q,
  r,
  side,
  road: "None",
  river: false,
  bridge: false,
  waterway: "None",
  setByUmpire: true,
  ...changes,
});

describe("the rule book's table", () => {
  it("is the figures' table", () => {
    expect(rulesTable).toEqual(figures.rules);
  });
});

describe.each(figures.cases)("stepCost: $name", (step) => {
  it("costs what the figures say, or is closed for their reason", () => {
    const cellAt = (q: number, r: number, cell: { terrain: string; forest: boolean }) => ({
      q,
      r,
      terrain: cell.terrain as CampaignGridResponse["cells"][number]["terrain"],
      forest: cell.forest,
      settlement: noSettlement,
      setByUmpire: true,
    });
    const terrain: CampaignGridResponse = {
      cells: [
        ...("fromCell" in step && step.fromCell ? [cellAt(0, 0, step.fromCell)] : []),
        ...("cell" in step && step.cell ? [cellAt(0, -1, step.cell)] : []),
      ],
      edges:
        "edge" in step && step.edge ? [edge(0, 0, "N", step.edge as Partial<HexEdgeResponse>)] : [],
    };

    const found = stepCost(rules, indexTerrain(terrain), classOf(step.type as UnitType), centre, {
      q: 0,
      r: -1,
    });

    if (step.cost === null) {
      expect(found).toEqual({ cost: Infinity, closedBecause: step.closedBecause });
    } else {
      expect(found.closedBecause).toBeNull();
      expect(found.cost).toBeCloseTo(step.cost, 12);
    }
  });
});

describe("stepCost", () => {
  it("finds an edge stored on the hex across it", () => {
    // Stepping south from (0, -1) crosses (0, 0)'s N edge from the other side.
    const terrain = indexTerrain({ cells: [], edges: [edge(0, 0, "N", { river: true })] });

    expect(stepCost(rules, terrain, "Infantry", { q: 0, r: -1 }, centre).closedBecause).toBe(
      "a river without a bridge is in the way",
    );
  });

  it("goes by the campaign's own table", () => {
    const faster = ratesOf({
      rules: false,
      rates: [{ class: "Infantry", ground: "Flat", hexes: 4 }],
    });

    expect(stepCost(faster, open, "Infantry", centre, { q: 0, r: -1 }).cost).toBe(0.25);
  });
});

describe("reach", () => {
  it("goes as far as the class's rate", () => {
    const found = reach(grid, centre, "LineInfantry", { rates: rules, terrain: open });

    // The start, its six neighbours and the twelve hexes around them: all in this grid.
    expect(found.size).toBe(19);
    expect(found.get(hexKey({ q: 2, r: -2 }))?.cost).toBe(1);
    expect(found.has(hexKey({ q: 3, r: -3 }))).toBe(false);
  });

  it("keeps inside the grid", () => {
    const found = reach(grid, { q: 0, r: -2 }, "LightCavalry", { rates: rules, terrain: open });

    expect(found.has(hexKey({ q: 0, r: -3 }))).toBe(false);
  });

  it("goes round closed steps, and less far into hills", () => {
    const terrain = indexTerrain({
      cells: [
        {
          q: 0,
          r: -1,
          terrain: "Water",
          forest: false,
          settlement: noSettlement,
          setByUmpire: true,
        },
        {
          q: 1,
          r: -1,
          terrain: "LowHill",
          forest: false,
          settlement: noSettlement,
          setByUmpire: true,
        },
      ],
      edges: [],
    });

    const found = reach(grid, centre, "LineInfantry", { rates: rules, terrain });

    expect(found.has(hexKey({ q: 0, r: -1 }))).toBe(false);
    expect(found.get(hexKey({ q: 1, r: -1 }))?.cost).toBe(1);
    // Beside the lake, round it by flat ground: two steps, a whole turn.
    expect(found.get(hexKey({ q: -1, r: -1 }))?.cost).toBe(1);
    expect(pathTo(found, { q: -1, r: -1 })).toEqual([
      { q: -1, r: 0 },
      { q: -1, r: -1 },
    ]);
  });

  it("goes anywhere in the grid without a budget, for the Umpire, closed steps and all", () => {
    const terrain = indexTerrain({
      cells: grid
        .hexes()
        .filter((hex) => hexKey(hex) !== hexKey(centre))
        .map((hex) => ({
          ...hex,
          terrain: "Water",
          forest: false,
          settlement: noSettlement,
          setByUmpire: true,
        })),
      edges: [],
    });

    expect(
      reach(grid, centre, "SupplyTrain", { rates: rules, terrain, budget: Infinity }).size,
    ).toBe(grid.hexes().length);
  });
});

describe("reach, into a hex that takes more than a turn", () => {
  const hills = indexTerrain({
    cells: [
      {
        q: 0,
        r: -1,
        terrain: "HighHill",
        forest: false,
        settlement: noSettlement,
        setByUmpire: true,
      },
    ],
    edges: [],
  });

  it("goes part of the way in with what's left of the turn", () => {
    const found = reach(grid, centre, "LineInfantry", { rates: rules, terrain: hills });

    // Straight in: half of it with the whole turn.
    expect(found.get(hexKey({ q: 0, r: -1 }))).toMatchObject({ progress: 0.5, from: "0,0" });
    // From two steps away, a flat hex first leaves half the turn: a quarter of the way in.
    const further = reach(grid, { q: 1, r: 0 }, "LineInfantry", { rates: rules, terrain: hills });
    expect(further.get(hexKey({ q: 0, r: -1 }))?.progress).toBe(0.25);
    expect(pathTo(found, { q: 0, r: -1 })).toEqual([{ q: 0, r: -1 }]);
  });

  it("carries on from how far it got last turn", () => {
    const found = reach(grid, centre, "LineInfantry", {
      rates: rules,
      terrain: hills,
      carried: { hex: { q: 0, r: -1 }, progress: 0.5 },
    });

    // The rest of the way in costs the whole turn: it gets there.
    expect(found.get(hexKey({ q: 0, r: -1 }))).toMatchObject({ cost: 1 });
    expect(found.get(hexKey({ q: 0, r: -1 }))?.progress).toBeUndefined();
  });

  it("isn't for hexes a turn or less away, or for the Umpire", () => {
    const flat = reach(grid, centre, "LineInfantry", { rates: rules, terrain: open });
    expect([...flat.values()].some((entry) => entry.progress !== undefined)).toBe(false);
    const umpire = reach(grid, centre, "LineInfantry", {
      rates: rules,
      terrain: hills,
      budget: Infinity,
    });
    expect(umpire.get(hexKey({ q: 0, r: -1 }))?.progress).toBeUndefined();
  });
});

describe("budgetFor", () => {
  const marches = { morningNations: ["France"], afternoonNations: ["Russia"] } as const;

  it("gives the named nations' infantry a flat hex's worth more each Morning, less each Afternoon", () => {
    expect(budgetFor(rules, "LineInfantry", "France", "Morning", marches)).toBe(1.5);
    expect(budgetFor(rules, "Engineers", "Russia", "Afternoon", marches)).toBe(0.5);
  });

  it("leaves everyone else, and the other times of day, at a turn", () => {
    expect(budgetFor(rules, "LineInfantry", "France", "Afternoon", marches)).toBe(1);
    expect(budgetFor(rules, "LineInfantry", "France", "Night", marches)).toBe(1);
    expect(budgetFor(rules, "LineInfantry", "Saxony", "Morning", marches)).toBe(1);
    expect(budgetFor(rules, "HeavyCavalry", "France", "Morning", marches)).toBe(1);
    expect(budgetFor(rules, "LineInfantry", "France", "Morning", undefined)).toBe(1);
  });

  it("goes by the campaign's flat rate", () => {
    const faster = ratesOf({
      rules: false,
      rates: [{ class: "Infantry", ground: "Flat", hexes: 4 }],
    });
    expect(budgetFor(faster, "LineInfantry", "France", "Morning", marches)).toBe(1.25);
  });
});

describe("shareOfTheWay", () => {
  it("reads the common shares in words", () => {
    expect(shareOfTheWay(0.5)).toBe("half");
    expect(shareOfTheWay(1 / 3)).toBe("a third");
    expect(shareOfTheWay(0.4)).toBe("40%");
  });
});

describe("pathTo", () => {
  it("gives the steps to a hex, the start left out", () => {
    const found = reach(grid, centre, "LineInfantry", { rates: rules, terrain: open });

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
