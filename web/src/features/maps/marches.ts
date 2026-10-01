import type { TurnPart, UnitMarchResponse, UnitType } from "@/api/generated/model";
import { classOf, type Rates } from "@/features/maps/movement";

/**
 * Forced marches (step 47, decision 0018), in the app: how much further a force march goes, when
 * a unit can make one, and a unit's march count and what moving costs, in words. The count itself
 * is the API's (`GET /api/armies/{id}/marches`).
 */

/** How much further a force march goes, as a share of the turn: a flat hex's worth for its class. */
export function forceMarchBonus(rates: Rates, type: UnitType) {
  const flat = rates(classOf(type), "Flat");
  return flat > 0 ? 1 / flat : 0;
}

/** Whether the unit can force march this turn: by day, and a class that moves over flat ground. */
export function canForceMarch(rates: Rates, type: UnitType, part: TurnPart | null | undefined) {
  return part !== "Night" && forceMarchBonus(rates, type) > 0;
}

/** Attrition as a multiple of the rules' scale, in words; null for none. */
export function attritionInWords(multiplier: number) {
  if (multiplier <= 0) return null;
  if (multiplier === 1) return "normal attrition";
  if (multiplier === 2) return "double attrition";
  return `${String(multiplier)}× attrition`;
}

const ordinal = (n: number) =>
  `${String(n)}${n % 10 === 1 && n % 100 !== 11 ? "st" : n % 10 === 2 && n % 100 !== 12 ? "nd" : n % 10 === 3 && n % 100 !== 13 ? "rd" : "th"}`;

/** "Its 2nd turn of forced march" for the turn that leaves it at this many. */
export const forcedMarchTurn = (turns: number) => `its ${ordinal(turns)} turn of forced march`;

/** A unit's march count as the open turn began, in words. */
export function describeMarch(march: UnitMarchResponse) {
  if (march.forcedMarchTurns > 0) {
    return `Force marching: ${String(march.forcedMarchTurns)} ${march.forcedMarchTurns === 1 ? "turn" : "turns"} to rest off (a Hold each).`;
  }
  if (march.movesInRow > 0) {
    return `Moved ${String(march.movesInRow)} ${march.movesInRow === 1 ? "turn" : "turns"} in a row: a third makes a forced march.`;
  }
  return "Rested.";
}

/** What moving this turn costs it, in words; null for nothing. */
export function moveCost(march: UnitMarchResponse | undefined, forceMarch: boolean) {
  if (!march) return null;
  const cost = attritionInWords(forceMarch ? march.forceMarchCosts : march.moveCosts);
  return cost ? `Moving costs ${cost}: ${forcedMarchTurn(march.forcedMarchTurns + 1)}.` : null;
}
