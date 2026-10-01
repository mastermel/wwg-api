import type { UnitType } from "@/api/generated/model";

/**
 * A campaign's concentration settings (step 46, decision 0017), as the API's Concentration: the
 * rules' limits (Campaign, §H), and the unit types usually counted towards each; the rest are free.
 */
export const rulesInfantryLimit = 200;
export const rulesCavalryLimit = 160;

export const usualInfantryTypes: readonly UnitType[] = [
  "LineInfantry",
  "LightInfantry",
  "Engineers",
  "Partisans",
  "FootArtillery",
];

export const usualCavalryTypes: readonly UnitType[] = [
  "LightCavalry",
  "MediumCavalry",
  "HeavyCavalry",
  "Scouts",
  "HorseArtillery",
];
