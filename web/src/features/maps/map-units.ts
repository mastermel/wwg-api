import type { DistanceUnit } from "@/api/generated/model";

const metresPer: Record<DistanceUnit, number> = { Kilometres: 1000, Miles: 1609.344 };

export const distanceUnitLabels: Record<DistanceUnit, { long: string; short: string }> = {
  Kilometres: { long: "Kilometres", short: "km" },
  Miles: { long: "Miles", short: "mi" },
};

/** Metres (as stored) in the campaign's unit, to a tenth. */
export const toUnit = (metres: number, unit: DistanceUnit) =>
  Math.round((metres / metresPer[unit]) * 10) / 10;

/** A distance in the campaign's unit, in whole metres (as the API takes it). */
export const toMetres = (distance: number, unit: DistanceUnit) =>
  Math.round(distance * metresPer[unit]);

/** The most a movement limit can be (the API's 1,000 km), in the campaign's unit. */
export const maxDistance = (unit: DistanceUnit) => Math.floor(toUnit(1_000_000, unit));

/** The smallest and largest hex size (the API's 500 m and 50 km), in the campaign's unit. */
export const minHexDistance = (unit: DistanceUnit) => Math.ceil(toUnit(500, unit) * 10) / 10;
export const maxHexDistance = (unit: DistanceUnit) => Math.floor(toUnit(50_000, unit));

/** The languages place names can be in (the API's list), for choosing one. */
export const labelLanguages = [
  { value: "local", label: "Each place's own" },
  { value: "en", label: "English" },
  { value: "fr", label: "French" },
  { value: "de", label: "German" },
  { value: "es", label: "Spanish" },
  { value: "it", label: "Italian" },
  { value: "pt", label: "Portuguese" },
  { value: "nl", label: "Dutch" },
  { value: "pl", label: "Polish" },
  { value: "ru", label: "Russian" },
  { value: "sv", label: "Swedish" },
  { value: "da", label: "Danish" },
];
