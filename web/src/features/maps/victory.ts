import type { HexSettlement, VictoryPointsMode } from "@/api/generated/model";

/**
 * Towns and victory points (step 50, decision 0021), in the app. The value mirrors the API's
 * `HexSettlement.Value`; who holds what, and the totals, are the API's scoreboard.
 */

/** What a settlement is worth by the rules: the highest that applies, and a capital's more. */
export function rulesValue(settlement: HexSettlement) {
  if (settlement.size === "None" && !settlement.fortress) return 0;
  const base = Math.max(
    settlement.size === "Town" ? 10 : 0,
    settlement.size === "City" ? 25 : 0,
    settlement.walled ? 35 : 0,
    settlement.fortress ? 50 : 0,
  );
  const capital = settlement.capital === "Capital" ? 25 : settlement.capital === "Minor" ? 10 : 0;
  return base + capital;
}

/**
 * What it's worth in this campaign: the Umpire's value, or else the rules' (or, where only the
 * settlements the Umpire gives points count, nothing).
 */
export const settlementValue = (settlement: HexSettlement, mode: VictoryPointsMode = "Rules") =>
  settlement.victoryPoints ?? (mode === "Chosen" ? 0 : rulesValue(settlement));

/** "1 point", "10 points". */
export const victoryPoints = (n: number) => `${String(n)} ${n === 1 ? "point" : "points"}`;
