import type { DepotResponse, SupplyState, UnitSupplyResponse } from "@/api/generated/model";

/**
 * Supply (step 48, decision 0019), in words. The rules are the API's (`GET
 * /api/campaigns/{id}/supply`); attrition starts with a unit's 7th turn in a row out of supply.
 */

/** Turns out of supply before attrition starts: it's from the 7th. */
export const graceTurns = 6;

const turns = (n: number) => `${String(n)} ${n === 1 ? "turn" : "turns"}`;

/** One state in words, for the unit drawer. */
function stateInWords(state: SupplyState, turnsOut: number, depot?: DepotResponse) {
  switch (state) {
    case "Supplied":
      return depot?.name ? `Supplied from ${depot.name}` : "Supplied";
    case "Unsupplied":
      // Cut off as the turn began, but not yet a whole turn counted.
      if (turnsOut === 0) return "Out of supply";
      return turnsOut > graceTurns
        ? `Out of supply: ${turns(turnsOut)}, losing points to attrition`
        : `Out of supply: ${turns(turnsOut)} (attrition from the ${String(graceTurns + 1)}th)`;
    case "Exempt":
      return "Doesn't need supply";
    case "LivingOffTheLand":
      return "Living off the land";
    case "Untracked":
      return "Not tracked: its army has no depots";
  }
}

/** A unit's supply in words: now, and after this turn's orders if that's different. */
export function describeSupply(supply: UnitSupplyResponse, depots: readonly DepotResponse[]) {
  const depot = depots.find((d) => d.id === supply.depotId);
  const now = stateInWords(supply.state, supply.unsuppliedTurns, depot);
  if (supply.nextState === supply.state && supply.nextState !== "Unsupplied") return `${now}.`;
  if (supply.nextState === supply.state && supply.state === "Unsupplied") {
    return `${now}; still after this turn's orders.`;
  }
  const next = stateInWords(supply.nextState, supply.nextUnsuppliedTurns).replace(/^./, (c) =>
    c.toLowerCase(),
  );
  return `${now}; after this turn's orders, ${next}.`;
}

/** Whether a unit's supply needs a warning: out of supply after this turn's orders. */
export const outOfSupplyNext = (supply: UnitSupplyResponse) => supply.nextState === "Unsupplied";
