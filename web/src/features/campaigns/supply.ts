import type { Nation, UnitType } from "@/api/generated/model";

/** A campaign's usual supply settings (step 48, decision 0019), as the API's SupplyRules. */
export const usualSupplyReach = 1;
export const maxSupplyReach = 3;

/** Partisans, light infantry, scouts and light cavalry (the rules, §G.5). */
export const usualExemptTypes: readonly UnitType[] = [
  "Partisans",
  "LightInfantry",
  "Scouts",
  "LightCavalry",
];

/** French forces living off the land (§G.5(b)). */
export const usualOffTheLandNations: readonly Nation[] = ["France"];
