import type { FactionResponse } from "@/api/generated/model";

/** The campaign's factions, as options for choosing one. */
export const factionOptions = (factions: readonly FactionResponse[] | undefined) =>
  (factions ?? []).map((faction) => ({ value: faction.id, label: faction.name }));
