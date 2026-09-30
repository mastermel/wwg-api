import type {
  DetailRelief,
  DominantFeature,
  Favorability,
  HexDetailResponse,
  HexFeatures,
} from "@/api/generated/model";

/** A hex's actual terrain (decision 0016; the rules, p. 57), in words. */

export const reliefLabels: Record<DetailRelief, string> = {
  Flat: "Flat",
  Rolling: "Rolling",
  Hilly: "Hilly",
  HighHills: "High hills",
};

/** Each feature, as it reads in a list ("a small village and small woods"). */
export const featureLabels: Record<keyof HexFeatures, string> = {
  scrub: "scrub",
  village: "a small village",
  woods: "small woods",
  forest: "forest",
  farms: "farms",
  fields: "fields",
  streams: "streams",
};

export const dominantLabels: Record<DominantFeature, string> = {
  None: "No dominant feature",
  SmallCastle: "A small castle",
  WeakFarmhouse: "A weak farmhouse",
  StrongFarmhouse: "A strong farmhouse",
};

export const favorabilityLabels: Record<Favorability, string> = {
  NotRolled: "Not rolled",
  Favorable: "Favourable",
  Neutral: "Neutral",
  Unfavorable: "Unfavourable",
};

export const noFeatures: HexFeatures = {
  scrub: false,
  village: false,
  woods: false,
  forest: false,
  farms: false,
  fields: false,
  streams: false,
};

/** "a, b and c". */
const list = (items: string[]) =>
  items.length < 2
    ? (items[0] ?? "")
    : `${items.slice(0, -1).join(", ")} and ${items.at(-1) ?? ""}`;

/**
 * A detail in a sentence or two: "Rolling: a small village and small woods. A small castle.
 * Favourable ground."
 */
export function describeDetail(
  detail: Pick<HexDetailResponse, "relief" | "features" | "dominant" | "favorability">,
) {
  const features = (Object.keys(featureLabels) as (keyof HexFeatures)[])
    .filter((key) => detail.features[key])
    .map((key) => featureLabels[key]);
  return [
    `${reliefLabels[detail.relief]}${features.length ? `: ${list(features)}` : ""}.`,
    detail.dominant === "None" ? null : `${dominantLabels[detail.dominant]}.`,
    detail.favorability === "NotRolled"
      ? null
      : `${favorabilityLabels[detail.favorability]} ground.`,
  ]
    .filter(Boolean)
    .join(" ");
}
