import type { ArmySummary, ArmyUnitResponse } from "@/api/generated/model";
import type { Hex } from "@/features/maps/hex-grid";
import { unitTypeLabels } from "@/features/units/unit-types";

/** A unit where it is on the map, with its army (for its colour): its hex, and that hex's centre. */
export interface PlacedUnit {
  unit: ArmyUnitResponse;
  army: ArmySummary;
  hex: Hex;
  latitude: number;
  longitude: number;
}

/** Units drawn as one marker: those that would overlap at the current zoom. */
export interface UnitStack {
  /** Stable while its first unit leads it. */
  key: string;
  units: PlacedUnit[];
  latitude: number;
  longitude: number;
}

/**
 * Groups units whose markers would overlap (within `radius` pixels at the current zoom) into
 * stacks, each at its first unit's position. Greedy, in the given order, which is enough for the
 * few dozen units of a campaign.
 */
export function stackUnits(
  units: readonly PlacedUnit[],
  project: (lngLat: [number, number]) => { x: number; y: number },
  radius = 26,
): UnitStack[] {
  const stacks: (UnitStack & { x: number; y: number })[] = [];
  for (const placed of units) {
    const { x, y } = project([placed.longitude, placed.latitude]);
    const near = stacks.find((stack) => Math.hypot(stack.x - x, stack.y - y) < radius);
    if (near) {
      near.units.push(placed);
    } else {
      stacks.push({
        key: placed.unit.id,
        units: [placed],
        latitude: placed.latitude,
        longitude: placed.longitude,
        x,
        y,
      });
    }
  }
  return stacks.map(({ key, units: members, latitude, longitude }) => ({
    key,
    units: members,
    latitude,
    longitude,
  }));
}

/** What a screen reader hears for a unit. */
export const describeUnit = ({ unit, army }: PlacedUnit) =>
  `${unit.name}, ${unitTypeLabels[unit.type]}, ${army.name}`;
