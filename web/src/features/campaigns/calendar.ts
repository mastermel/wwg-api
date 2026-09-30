import type { CampaignTurnSummary, Nation, TurnPart } from "@/api/generated/model";

/** A campaign's calendar (step 45): its turns' days and times of day. */

export const turnPartLabels: Record<TurnPart, string> = {
  Morning: "Morning",
  Afternoon: "Afternoon",
  Night: "Night",
};

/** The hours each time of day covers (the rules, §D.1). */
export const turnPartHours: Record<TurnPart, string> = {
  Morning: "06:00–14:00",
  Afternoon: "14:00–22:00",
  Night: "22:00–06:00",
};

/**
 * The rule book's marching nations (§E.1), as the API's TurnParts: French infantry and their
 * usual allies a flat hex further each Morning; Russians and Austrians one less each Afternoon.
 */
export const rulesMorningNations: readonly Nation[] = [
  "France",
  "Bavaria",
  "Wurttemberg",
  "Baden",
  "Warsaw",
  "Italy",
  "Holland",
];
export const rulesAfternoonNations: readonly Nation[] = ["Russia", "Austria"];

const dayFormat = new Intl.DateTimeFormat("en-GB", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: "UTC",
});

/** A day from the API ("1815-06-17"), as the rule book writes it: "17 June 1815". */
export const formatDay = (day: string) => dayFormat.format(new Date(`${day}T00:00:00Z`));

/** When a turn falls: "17 June 1815, Afternoon", "Afternoon" without a start date; null for the setup. */
export function turnWhen(turn: Pick<CampaignTurnSummary, "part" | "date">): string | null {
  if (!turn.part) return null;
  return turn.date
    ? `${formatDay(turn.date)}, ${turnPartLabels[turn.part]}`
    : turnPartLabels[turn.part];
}
