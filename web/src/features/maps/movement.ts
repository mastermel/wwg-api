import type { UnitType } from "@/api/generated/model";
import { hexKey, neighbours, type Hex, type HexGrid } from "@/features/maps/hex-grid";

/**
 * How far units move in a turn, as the API's Movement.cs (DESIGN.md §5.2, Phase 11): a turn's
 * budget is 1, and entering a hex costs 1 ÷ the class's rate. Until terrain exists every hex is
 * flat ground.
 */

/** The rule book's movement classes. */
export type MovementClass = "Infantry" | "Light" | "LightCavalry" | "Cavalry" | "Slow";

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

/** The rules' rates on flat ground, in hexes per turn. */
const flatRates: Record<MovementClass, number> = {
  Infantry: 2,
  Light: 3,
  LightCavalry: 4,
  Cavalry: 3,
  Slow: 1,
};

export const classOf = (type: UnitType) => classes[type];

/** How many flat hexes a unit of this type moves in a turn. */
export const flatRate = (type: UnitType) => flatRates[classOf(type)];

// Sums of thirds don't come to exactly 1.
const tolerance = 1e-9;

/** Where a unit could go: each hex's cheapest cost from the start, and the hex it's reached from. */
export type Reach = ReadonlyMap<string, { hex: Hex; cost: number; from: string | null }>;

/**
 * Every hex in the grid a unit of `type` can reach from `start`, within `budget` turns' movement
 * (Infinity: anywhere, for the Umpire), by the cheapest way (Dijkstra; every step costs the same
 * until terrain comes, but the costs will differ then).
 */
export function reach(grid: HexGrid, start: Hex, type: UnitType, budget = 1): Reach {
  const step = 1 / flatRate(type);
  const found = new Map<string, { hex: Hex; cost: number; from: string | null }>([
    [hexKey(start), { hex: start, cost: 0, from: null }],
  ]);
  const queue: { hex: Hex; cost: number }[] = [{ hex: start, cost: 0 }];
  while (queue.length > 0) {
    queue.sort((a, b) => a.cost - b.cost);
    const { hex, cost } = queue.shift() ?? { hex: start, cost: 0 };
    if (cost > (found.get(hexKey(hex))?.cost ?? Infinity)) continue;
    for (const next of neighbours(hex)) {
      const nextCost = cost + step;
      const key = hexKey(next);
      if (nextCost > budget + tolerance || !grid.contains(next)) continue;
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
