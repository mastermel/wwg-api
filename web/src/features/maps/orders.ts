import type { UnitPosition } from "@/api/generated/model";
import type { Point } from "@/features/maps/geo";
import { boatCount } from "@/features/maps/boats";
import { shareOfTheWay } from "@/features/maps/movement";

/** A count of hexes, for reading: "1 hex", "2 hexes". */
export const hexes = (count: number) => `${String(count)} ${count === 1 ? "hex" : "hexes"}`;

/**
 * A unit's order this turn, in words: "Moves 2 hexes" (", by force march"; ", by boat"), "Holds",
 * "Embarks on 2 boats", "Lands", "Builds a boat", or "No order yet"; "Not on the map yet" for a
 * unit the Umpire hasn't placed (it can't have one). A boat tied to a unit "Goes with its unit".
 * A move from before the grid, with no path, just "Moves".
 */
export function describeOrder(
  order: UnitPosition | undefined,
  from: Point | undefined,
  night = false,
) {
  if (!from) return "Not on the map yet";
  if (!order) return "No order yet";
  // A move by night counts towards a forced march (step 45), as a force march is one (step 47).
  const living = order.livesOffTheLand ? ", living off the land" : "";
  // Boats (step 51): a tied boat's order is its unit's.
  if (order.carriedBy) return "Goes with its unit";
  if (order.kind === "Embark") return `Embarks on ${boatCount(order.boats.length)}`;
  if (order.kind === "Disembark")
    return `${order.path.length > 0 ? "Lands in the next hex" : "Lands"}${living}`;
  if (order.kind === "BuildBoat") return `Builds a boat${living}`;
  if (order.kind === "Hold") return `Holds${living}`;
  if (order.boats.length > 0) return `${describeMove(order)}, by boat`;
  if (night) return `${describeMove(order)}, by night${living}`;
  if (order.forceMarch) return `${describeMove(order)}, by force march${living}`;
  return `${describeMove(order)}${living}`;
}

function describeMove(order: UnitPosition) {
  if (order.progress !== null) {
    // The last hex takes more than a turn: it gets part of the way in (step 44).
    const into = `${shareOfTheWay(order.progress)} of the way into the next`;
    return order.path.length > 1
      ? `Moves ${hexes(order.path.length - 1)}, and ${into}`
      : `Goes ${into}`;
  }
  return order.path.length > 0 ? `Moves ${hexes(order.path.length)}` : "Moves";
}
