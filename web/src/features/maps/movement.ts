import type { Ground, MovementClass, MovementTableResponse, UnitType } from "@/api/generated/model";
import { directions, hexKey, neighbours, type Hex, type HexGrid } from "@/features/maps/hex-grid";
import { sides, storedEdge, type TerrainIndex } from "@/features/maps/terrain";

/**
 * How far units move in a turn, as the API's Movement.cs (DESIGN.md §5.2; decision 0014, step
 * 44): a turn's budget is 1, and entering a hex costs 1 ÷ the class's rate for the step, by the
 * campaign's movement table. Both are tested against testdata/movement.json.
 */

const classes: Record<UnitType, MovementClass> = {
  LineInfantry: "Infantry",
  FootArtillery: "Infantry",
  Engineers: "Infantry",
  LightInfantry: "Light",
  Partisans: "Light",
  LightCavalry: "LightCavalry",
  Scouts: "LightCavalry",
  MediumCavalry: "Cavalry",
  HeavyCavalry: "Cavalry",
  HorseArtillery: "Cavalry",
  SupplyTrain: "Slow",
  SiegeArtillery: "Slow",
};

export const classOf = (type: UnitType) => classes[type];

export const movementClasses: readonly MovementClass[] = [
  "Infantry",
  "Light",
  "LightCavalry",
  "Cavalry",
  "Slow",
];

export const grounds: readonly Ground[] = [
  "GoodRoad",
  "PoorRoad",
  "Flat",
  "LowHill",
  "HighHill",
  "Mountain",
];

export const classLabels: Record<MovementClass, string> = {
  Infantry: "Infantry and foot artillery",
  Light: "Light infantry and partisans",
  LightCavalry: "Light cavalry and scouts",
  Cavalry: "Cavalry and horse artillery",
  Slow: "Supply trains and siege artillery",
};

export const groundLabels: Record<Ground, string> = {
  GoodRoad: "Good road",
  PoorRoad: "Poor road",
  Flat: "Flat",
  LowHill: "Low hills",
  HighHill: "High hills",
  Mountain: "Mountains",
};

/** In the API's closed-step reasons: "infantry can't cross mountains". */
const classWords: Record<MovementClass, string> = {
  Infantry: "infantry",
  Light: "light infantry",
  LightCavalry: "light cavalry",
  Cavalry: "cavalry",
  Slow: "supply trains and siege artillery",
};

const groundWords: Partial<Record<Ground, string>> = {
  Mountain: "mountains",
  HighHill: "high hills",
  LowHill: "low hills",
};

/** The rule book's table (§E.1), in hexes a turn; 0: can't. */
export const rulesTable: Record<MovementClass, Record<Ground, number>> = {
  Infantry: { GoodRoad: 3, PoorRoad: 2, Flat: 2, LowHill: 1, HighHill: 0.5, Mountain: 0 },
  Light: { GoodRoad: 5, PoorRoad: 4, Flat: 3, LowHill: 2, HighHill: 1, Mountain: 0.5 },
  LightCavalry: { GoodRoad: 6, PoorRoad: 5, Flat: 4, LowHill: 3, HighHill: 2, Mountain: 1 },
  Cavalry: { GoodRoad: 5, PoorRoad: 4, Flat: 3, LowHill: 2, HighHill: 1, Mountain: 0 },
  Slow: { GoodRoad: 3, PoorRoad: 2, Flat: 1, LowHill: 0.5, HighHill: 0, Mountain: 0 },
};

/** How many hexes a turn a class moves on a ground. */
export type Rates = (movementClass: MovementClass, ground: Ground) => number;

/** The campaign's table as rates: the rules' until it's loaded. */
export function ratesOf(table: MovementTableResponse | undefined): Rates {
  const own = new Map(table?.rates.map((r) => [`${r.class}/${r.ground}`, r.hexes]));
  return (movementClass, ground) =>
    own.get(`${movementClass}/${ground}`) ?? rulesTable[movementClass][ground];
}

/** What a step costs, as a share of a turn; closed (with the API's reason), it's Infinity. */
export interface StepCost {
  cost: number;
  closedBecause: string | null;
}

const closed = (because: string): StepCost => ({ cost: Infinity, closedBecause: because });

/**
 * What entering `to` from `from` costs, as the API's Movement.Step: the hex's ground (a forest
 * moves as low hills, or its own hills if higher), or the road across the edge when that's
 * quicker (a good road into high hills or mountains counts as poor). Water, a river along the
 * edge without a bridge, and ground the class can't cross (0) close the step.
 */
export function stepCost(
  rates: Rates,
  terrain: TerrainIndex,
  movementClass: MovementClass,
  from: Hex,
  to: Hex,
): StepCost {
  const direction = directions.findIndex((d) => from.q + d.q === to.q && from.r + d.r === to.r);
  if (direction < 0) return closed("each step must be to the next hex");
  const cell = terrain.cell(to);
  if (cell?.terrain === "Water") return closed("it's water");
  const edge = terrain.edge(storedEdge(from, sides[direction] ?? "N"));
  if (edge?.river && !edge.bridge) return closed("a river without a bridge is in the way");

  const land: Ground =
    cell?.terrain === "Mountain"
      ? "Mountain"
      : cell?.terrain === "HighHill"
        ? "HighHill"
        : cell?.terrain === "LowHill" || cell?.forest
          ? "LowHill"
          : "Flat";
  const road: Ground | null =
    edge?.road === "Good"
      ? land === "HighHill" || land === "Mountain"
        ? "PoorRoad"
        : "GoodRoad"
      : edge?.road === "Poor"
        ? "PoorRoad"
        : null;
  const rate = Math.max(rates(movementClass, land), road ? rates(movementClass, road) : 0);
  return rate > 0
    ? { cost: 1 / rate, closedBecause: null }
    : closed(`${classWords[movementClass]} can't cross ${groundWords[land] ?? "that ground"}`);
}

// Sums of thirds don't come to exactly 1.
const tolerance = 1e-9;

/** For the Umpire, who may cross a closed step (decision 0011): dear, so it's the last resort. */
const closedForUmpire = 100;

/** Where a unit could go: each hex's cheapest cost from the start, and the hex it's reached from. */
export type Reach = ReadonlyMap<string, { hex: Hex; cost: number; from: string | null }>;

export interface ReachOptions {
  rates: Rates;
  terrain: TerrainIndex;
  /** Turns' worth of movement (Infinity: anywhere, for the Umpire, closed steps and all). */
  budget?: number;
}

/**
 * Every hex in the grid a unit of `type` can reach from `start` within the budget, by the
 * cheapest way (Dijkstra), as the API costs it.
 */
export function reach(
  grid: HexGrid,
  start: Hex,
  type: UnitType,
  { rates, terrain, budget = 1 }: ReachOptions,
): Reach {
  const movementClass = classOf(type);
  const found = new Map<string, { hex: Hex; cost: number; from: string | null }>([
    [hexKey(start), { hex: start, cost: 0, from: null }],
  ]);
  const queue: { hex: Hex; cost: number }[] = [{ hex: start, cost: 0 }];
  while (queue.length > 0) {
    queue.sort((a, b) => a.cost - b.cost);
    const { hex, cost } = queue.shift() ?? { hex: start, cost: 0 };
    if (cost > (found.get(hexKey(hex))?.cost ?? Infinity)) continue;
    for (const next of neighbours(hex)) {
      if (!grid.contains(next)) continue;
      const step = stepCost(rates, terrain, movementClass, hex, next);
      const nextCost =
        cost + (step.closedBecause !== null && budget === Infinity ? closedForUmpire : step.cost);
      const key = hexKey(next);
      if (nextCost > budget + tolerance) continue;
      if (nextCost < (found.get(key)?.cost ?? Infinity) - tolerance) {
        found.set(key, { hex: next, cost: nextCost, from: hexKey(hex) });
        queue.push({ hex: next, cost: nextCost });
      }
    }
  }
  return found;
}

/** The cheapest path to `target` in a reach, as the order sends it (the start left out); null if out of reach. */
export function pathTo(found: Reach, target: Hex): Hex[] | null {
  const path: Hex[] = [];
  let at = found.get(hexKey(target));
  if (!at) return null;
  while (at.from !== null) {
    path.unshift(at.hex);
    at = found.get(at.from);
    if (!at) return null;
  }
  return path;
}

/** Whether a cost is within a turn. */
export const affordable = (cost: number) => cost <= 1 + tolerance;
