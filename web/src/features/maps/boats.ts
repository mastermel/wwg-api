import type { UnitPosition, UnitType } from "@/api/generated/model";
import { hexKey, neighbours, type Hex } from "@/features/maps/hex-grid";
import { sides, storedEdge, type TerrainIndex } from "@/features/maps/terrain";
import type { PlacedUnit } from "@/features/maps/stacks";

/**
 * Boats (step 51, decision 0022), in the app; the API's rules (`BoatRules`, `Embarkation`,
 * `BoatBuilding`) decide. A unit embarks on its army's free boats in its hex, which are tied to it
 * and go with it; it moves as boats do, and lands in its hex, across a river side or onto a
 * lake's shore. Boats are built in a settlement on a waterway.
 */

/** Chart #11's boat, until the Umpire sets another. */
export const usualBoatCapacity = 14;

/** The boats a unit of these points needs: one for each capacity's worth, or part. */
export const boatsNeeded = (points: number, capacity: number) =>
  Math.max(1, Math.ceil(points / capacity));

/** Whether a unit of this type boards boats: not boats, nor supply trains. */
export const canEmbark = (type: UnitType) => type !== "Boat" && type !== "SupplyTrain";

/** The boats an order leaves its unit on: any but a landing's. */
export const aboardAfter = (order: Pick<UnitPosition, "kind" | "boats">): readonly string[] =>
  order.kind === "Disembark" ? [] : order.boats;

/** The boats tied to units on the map: none of them is drawn, or ordered, on its own. */
export function tiedBoats(units: readonly PlacedUnit[]): Set<string> {
  return new Set(units.flatMap((u) => u.boats ?? []));
}

/**
 * The army's free boats in the unit's hex, by name: none tied to a unit, nor boarded by another
 * this turn (its `orders`).
 */
export function freeBoatsFor(
  placed: PlacedUnit,
  units: readonly PlacedUnit[],
  orders: readonly UnitPosition[],
): PlacedUnit[] {
  const tied = tiedBoats(units);
  const boarded = new Set(
    orders
      .filter((o) => o.kind === "Embark" && o.unitId !== placed.unit.id)
      .flatMap((o) => o.boats),
  );
  return units
    .filter(
      (u) =>
        u.unit.type === "Boat" &&
        u.army.id === placed.army.id &&
        hexKey(u.hex) === hexKey(placed.hex) &&
        !tied.has(u.unit.id) &&
        !boarded.has(u.unit.id),
    )
    .sort((a, b) => a.unit.name.localeCompare(b.unit.name));
}

/**
 * Where a unit on boats can land: its own hex, unless it's water; a neighbour across a river
 * side; and from a lake (a Water hex), a neighbour that isn't water.
 */
export function landingHexes(from: Hex, terrain: TerrainIndex): Hex[] {
  const water = (hex: Hex) => terrain.cell(hex)?.terrain === "Water";
  const around = neighbours(from);
  return [
    ...(water(from) ? [] : [from]),
    ...sides.flatMap((side, i) => {
      const to = around[i] ?? from;
      if (water(to)) return [];
      return terrain.edge(storedEdge(from, side))?.river || water(from) ? [to] : [];
    }),
  ];
}

/** Whether boats can be built in a hex: a town, city or fortress with a waterway along a side. */
export function canBuildBoatsAt(hex: Hex, terrain: TerrainIndex) {
  const settlement = terrain.cell(hex)?.settlement;
  if (!settlement || (settlement.size === "None" && !settlement.fortress)) return false;
  return sides.some((side) => (terrain.edge(storedEdge(hex, side))?.waterway ?? "None") !== "None");
}

/** "1 boat", "3 boats". */
export const boatCount = (n: number) => `${String(n)} ${n === 1 ? "boat" : "boats"}`;
