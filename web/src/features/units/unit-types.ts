import { UnitType } from "@/api/generated/model";

/** How each unit type reads in the app. */
export const unitTypeLabels: Record<UnitType, string> = {
  LineInfantry: "Line Infantry",
  FootArtillery: "Foot Artillery",
  Engineers: "Engineers",
  LightInfantry: "Light Infantry",
  Partisans: "Partisans",
  LightCavalry: "Light Cavalry",
  Scouts: "Scouts",
  MediumCavalry: "Medium Cavalry",
  HeavyCavalry: "Heavy Cavalry",
  HorseArtillery: "Horse Artillery",
  SupplyTrain: "Supply Train",
  SiegeArtillery: "Siege Artillery",
};

/** The types, in the API's order, for a Select. */
export const unitTypeOptions = Object.values(UnitType).map((value) => ({
  value,
  label: unitTypeLabels[value],
}));
