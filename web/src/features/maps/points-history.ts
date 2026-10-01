import type { PointsChangeResponse } from "@/api/generated/model";

/** "−3" or "+2", with a real minus sign. */
const signed = (n: number) => (n < 0 ? `−${String(-n)}` : `+${String(n)}`);

/** A change to a unit's points, in words: "Turn 4: −3, to 47. Forced march, ×2." */
export function describePointsChange(change: PointsChangeResponse) {
  const why =
    change.reason === "Attrition"
      ? (change.note ?? "Attrition")
      : `Changed by ${change.byName ?? "the Umpire"}`;
  return `Turn ${String(change.turn)}: ${signed(change.change)}, to ${String(change.pointsAfter)}. ${why}.`;
}
