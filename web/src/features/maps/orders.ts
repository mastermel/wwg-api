import type { DistanceUnit, UnitPosition } from "@/api/generated/model";
import { distanceMetres, type Point } from "@/features/maps/geo";
import { distanceUnitLabels, toUnit } from "@/features/maps/map-units";

/** A distance in the campaign's unit, for reading: "3.3 km". */
export const formatDistance = (metres: number, unit: DistanceUnit) =>
  `${String(toUnit(metres, unit))} ${distanceUnitLabels[unit].short}`;

/**
 * A unit's order this turn, in words: "Moves 3.3 km", "Holds" or "No order yet"; "Not on the map
 * yet" for a unit the Umpire hasn't placed (it can't have one).
 */
export function describeOrder(
  order: UnitPosition | undefined,
  from: Point | undefined,
  unit: DistanceUnit,
) {
  if (!from) return "Not on the map yet";
  if (!order) return "No order yet";
  return order.kind === "Hold"
    ? "Holds"
    : `Moves ${formatDistance(distanceMetres(from, order), unit)}`;
}
