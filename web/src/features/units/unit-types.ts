import { UnitType } from "@/api/generated/model";

/** How each unit type reads in the app. */
export const unitTypeLabels: Record<UnitType, string> = {
  HeavyInfantry: "Heavy Infantry",
  LightInfantry: "Light Infantry",
  Skirmishers: "Skirmishers",
  HeavyCavalry: "Heavy Cavalry",
  LightCavalry: "Light Cavalry",
  FootArtillery: "Foot Artillery",
  HorseArtillery: "Horse Artillery",
};

/** The types, in the API's order, for a Select. */
export const unitTypeOptions = Object.values(UnitType).map((value) => ({
  value,
  label: unitTypeLabels[value],
}));
