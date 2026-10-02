import type {
  ForceSize,
  SightedUnitResponse,
  SightingDueResponse,
  SightingRequest,
  SightingStrength,
} from "@/api/generated/model";
import { suggestedSize } from "@/features/maps/sightings";

/** One sighting in the start-turn list, as the Umpire is shaping it. */
export interface SightingEntry {
  observingArmyId: string;
  q: number;
  r: number;
  whereabouts: string;
  screened: boolean;
  units: readonly SightedUnitResponse[];
  /** Added by the Umpire rather than found. */
  byHand: boolean;
  include: boolean;
  showsHex: boolean;
  showsArmies: boolean;
  showsTypes: boolean;
  /** Whether it says the force was on boats (step 51); offered only when it was. */
  showsAfloat: boolean;
  strength: SightingStrength;
  size: ForceSize;
}

const pointsOf = (units: readonly { points: number }[]) =>
  units.reduce((sum, u) => sum + u.points, 0);

/** Each sighting the app found, prefilled: everything shown, with a rough size (decision 0020). */
export const initialEntries = (due: readonly SightingDueResponse[]): SightingEntry[] =>
  due.map((d) => ({
    observingArmyId: d.observingArmyId,
    q: d.q,
    r: d.r,
    whereabouts: d.whereabouts,
    screened: d.screened,
    units: d.units,
    byHand: false,
    include: true,
    showsHex: true,
    showsArmies: true,
    showsTypes: true,
    showsAfloat: d.units.some((u) => u.afloat),
    strength: "Rough",
    size: suggestedSize(pointsOf(d.units)),
  }));

/** The sightings to send: those included, as shaped. */
export const toRequests = (entries: readonly SightingEntry[]): SightingRequest[] =>
  entries
    .filter((e) => e.include)
    .map((e) => ({
      observingArmyId: e.observingArmyId,
      q: e.q,
      r: e.r,
      showsHex: e.showsHex,
      showsArmies: e.showsArmies,
      showsTypes: e.showsTypes,
      showsAfloat: e.showsAfloat && e.units.some((u) => u.afloat),
      strength: e.strength,
      size: e.strength === "Rough" ? e.size : null,
    }));
