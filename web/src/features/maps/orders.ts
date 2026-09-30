import type { UnitPosition } from "@/api/generated/model";
import type { Point } from "@/features/maps/geo";
import { shareOfTheWay } from "@/features/maps/movement";

/** A count of hexes, for reading: "1 hex", "2 hexes". */
export const hexes = (count: number) => `${String(count)} ${count === 1 ? "hex" : "hexes"}`;

/**
 * A unit's order this turn, in words: "Moves 2 hexes", "Holds" or "No order yet"; "Not on the map
 * yet" for a unit the Umpire hasn't placed (it can't have one). A move from before the grid, with
 * no path, just "Moves".
 */
export function describeOrder(order: UnitPosition | undefined, from: Point | undefined) {
  if (!from) return "Not on the map yet";
  if (!order) return "No order yet";
  if (order.kind === "Hold") return "Holds";
  if (order.progress !== null) {
    // The last hex takes more than a turn: it gets part of the way in (step 44).
    const into = `${shareOfTheWay(order.progress)} of the way into the next`;
    return order.path.length > 1
      ? `Moves ${hexes(order.path.length - 1)}, and ${into}`
      : `Goes ${into}`;
  }
  return order.path.length > 0 ? `Moves ${hexes(order.path.length)}` : "Moves";
}
