import type {
  CampaignGridResponse,
  CapitalStatus,
  EdgeSide,
  HexCellResponse,
  HexEdgeResponse,
  HexSettlement,
  RoadQuality,
  SettlementSize,
  Terrain,
  Waterway,
} from "@/api/generated/model";
import { directions, hexKey, type Hex, type HexGrid } from "@/features/maps/hex-grid";

/**
 * A campaign's terrain on its hex grid (decisions 0014 and 0016), as the API stores it: hexes and
 * edges with something on them. An edge is stored on one hex's N, NE or SE side; a hex's other
 * three sides are its neighbours' (its S is the N of the hex below).
 */

export const terrainLabels: Record<Terrain, string> = {
  Flat: "Flat",
  LowHill: "Low hills",
  HighHill: "High hills",
  Mountain: "Mountains",
  Water: "Water",
};

export const settlementSizeLabels: Record<SettlementSize, string> = {
  None: "None",
  Town: "Town",
  City: "City",
};

export const capitalLabels: Record<CapitalStatus, string> = {
  None: "Not a capital",
  Minor: "Minor capital",
  Capital: "Capital",
};

export const roadLabels: Record<RoadQuality, string> = {
  None: "No road",
  Poor: "Poor road",
  Good: "Good road",
};

/** A hex's six sides, clockwise from north: the order of `directions`. */
export const sides = ["N", "NE", "SE", "S", "SW", "NW"] as const;
export type Side = (typeof sides)[number];

export const sideLabels: Record<Side, string> = {
  N: "North",
  NE: "North-east",
  SE: "South-east",
  S: "South",
  SW: "South-west",
  NW: "North-west",
};

/** Where a hex's side is stored: on which hex and which of its N, NE and SE sides. */
export interface StoredEdge {
  q: number;
  r: number;
  side: EdgeSide;
  /**
   * Whether it's stored on the neighbour: then a waterway flowing Out of the stored hex flows
   * into this one.
   */
  flipped: boolean;
}

const stored: EdgeSide[] = ["N", "NE", "SE"];

/** Where a hex's side is stored. */
export function storedEdge(hex: Hex, side: Side): StoredEdge {
  const index = sides.indexOf(side);
  if (index < 3) return { q: hex.q, r: hex.r, side: stored[index] ?? "N", flipped: false };
  // S, SW and NW are the N, NE and SE of the hex across them.
  const d = directions[index] ?? { q: 0, r: 0 };
  return { q: hex.q + d.q, r: hex.r + d.r, side: stored[index - 3] ?? "N", flipped: true };
}

export const edgeKey = ({ q, r, side }: { q: number; r: number; side: EdgeSide }) =>
  `${String(q)},${String(r)},${side}`;

/** The hex across a stored edge from the hex it's stored on. */
export function acrossStored({ q, r, side }: { q: number; r: number; side: EdgeSide }): Hex {
  const d = directions[stored.indexOf(side)] ?? { q: 0, r: 0 };
  return { q: q + d.q, r: r + d.r };
}

/** The flow as the hex sees it, whichever hex the edge is stored on. */
export const flowFor = (waterway: Waterway, flipped: boolean): Waterway =>
  !flipped || waterway === "None" ? waterway : waterway === "Out" ? "In" : "Out";

/** A hex's side's two corners, as [longitude, latitude]. */
export function sideCorners(grid: HexGrid, hex: Hex, side: Side): [number, number][] {
  // The grid's corners run clockwise from east: E, SE, SW, W, NW, NE.
  const corners = grid.corners(hex);
  const [a, b] = (
    { N: [4, 5], NE: [5, 0], SE: [0, 1], S: [1, 2], SW: [2, 3], NW: [3, 4] } as const
  )[side];
  return [corners[a] ?? [0, 0], corners[b] ?? [0, 0]];
}

/** The side of `hex` nearest a point in it: the one its bearing from the centre points at. */
export function nearestSide(
  grid: HexGrid,
  hex: Hex,
  point: { longitude: number; latitude: number },
) {
  const centre = grid.centre(hex);
  const east = (point.longitude - centre.longitude) * Math.cos((centre.latitude * Math.PI) / 180);
  const north = point.latitude - centre.latitude;
  // Degrees clockwise from north; each side spans 60°, N centred on 0.
  const bearing = ((Math.atan2(east, north) * 180) / Math.PI + 360) % 360;
  return sides[Math.round(bearing / 60) % 6] ?? "N";
}

/** The terrain, looked up by hex and by stored edge. */
export interface TerrainIndex {
  cell: (hex: Hex) => HexCellResponse | undefined;
  edge: (edge: { q: number; r: number; side: EdgeSide }) => HexEdgeResponse | undefined;
}

export function indexTerrain(grid: CampaignGridResponse | undefined): TerrainIndex {
  const cells = new Map((grid?.cells ?? []).map((c) => [hexKey(c), c]));
  const edges = new Map((grid?.edges ?? []).map((e) => [edgeKey(e), e]));
  return { cell: (hex) => cells.get(hexKey(hex)), edge: (edge) => edges.get(edgeKey(edge)) };
}

export const noSettlement: HexSettlement = {
  size: "None",
  walled: false,
  fortress: false,
  capital: "None",
  name: null,
};

/** A settlement in words: "Walled capital city, with a fortress: Brussels". */
export function describeSettlement(settlement: HexSettlement): string | null {
  const { size, walled, fortress, capital, name } = settlement;
  if (size === "None" && !fortress) return null;
  const kind =
    size === "None"
      ? "Fortress"
      : [
          walled ? "Walled" : null,
          capital === "Capital" ? "capital" : capital === "Minor" ? "minor capital" : null,
          size === "City" ? "city" : "town",
        ]
          .filter(Boolean)
          .join(" ")
          .replace(/^./, (c) => c.toUpperCase()) + (fortress ? ", with a fortress" : "");

  return name ? `${name}: ${kind}` : kind;
}
