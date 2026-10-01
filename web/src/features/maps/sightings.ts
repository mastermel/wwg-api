import type { ArmySummary, ForceSize, SightingResponse, UnitType } from "@/api/generated/model";
import { hexName } from "@/features/maps/hex-grid";
import { unitTypeLabels } from "@/features/units/unit-types";

/**
 * Sightings (step 49, decision 0020), in words, and which to draw. A sighting belongs to its turn:
 * drawn in full on it, faded for the 3 turns after.
 */

/** How many turns after its own a sighting is still drawn, faded. */
export const fadingTurns = 3;

export const forceSizeLabels: Record<ForceSize, string> = {
  Small: "a small force",
  Medium: "a medium force",
  Large: "a large force",
};

/** The size the Umpire is offered first, by the force's points. */
export const suggestedSize = (points: number): ForceSize =>
  points < 50 ? "Small" : points < 150 ? "Medium" : "Large";

/** The sightings to draw when viewing a turn: its own (in full) and the 3 turns' before (faded). */
export function sightingsFor(sightings: readonly SightingResponse[], turn: number) {
  return sightings
    .filter((s) => s.turn <= turn && s.turn >= turn - fadingTurns)
    .map((s) => ({ sighting: s, faded: s.turn !== turn }));
}

/** "2 line infantry and 1 light cavalry". */
export function describeTypes(types: readonly UnitType[]) {
  const counts = new Map<UnitType, number>();
  for (const type of types) counts.set(type, (counts.get(type) ?? 0) + 1);
  const parts = [...counts].map(
    ([type, n]) => `${String(n)} ${unitTypeLabels[type].toLowerCase()}`,
  );
  return parts.length < 2
    ? (parts[0] ?? "")
    : `${parts.slice(0, -1).join(", ")} and ${parts.at(-1) ?? ""}`;
}

/** A sighting in a sentence: where, whose, what, how strong, and how it came. */
export function describeSighting(sighting: SightingResponse, armies: readonly ArmySummary[]) {
  const where =
    sighting.q !== null && sighting.r !== null
      ? hexName({ q: sighting.q, r: sighting.r })
      : sighting.whereabouts;
  const whose = sighting.armyIds
    ? sighting.armyIds.map((id) => armies.find((a) => a.id === id)?.name ?? "an army").join(" and ")
    : "Enemy troops";
  const what = sighting.unitTypes ? `: ${describeTypes(sighting.unitTypes)}` : "";
  const strength =
    sighting.strength === "Exact" && sighting.points !== null
      ? `, ${String(sighting.points)} points`
      : sighting.strength === "Rough" && sighting.size
        ? `, ${forceSizeLabels[sighting.size]}`
        : "";
  const source = sighting.sharedByArmyId
    ? ` (from ${armies.find((a) => a.id === sighting.sharedByArmyId)?.name ?? "an ally"}'s report)`
    : sighting.byUmpire
      ? " (reported)"
      : "";
  return `${where}: ${whose}${what}${strength}${source}.`;
}
