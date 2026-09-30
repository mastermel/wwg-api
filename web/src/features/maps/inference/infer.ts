import type {
  CapitalStatus,
  InferredHexCell,
  InferredHexEdge,
  RoadQuality,
  SettlementSize,
  Terrain,
  Waterway,
} from "@/api/generated/model";
import type { Point } from "@/features/maps/geo";
import { directions, hexKey, neighbours, type Hex, type HexGrid } from "@/features/maps/hex-grid";
import { edgeKey, noSettlement, storedEdge, type Side } from "@/features/maps/terrain";

/**
 * Terrain inferred from the map's data (decisions 0014 and 0016), hex by hex, for the Umpire to
 * save and correct. What the data is read from is `TerrainSources` (`sources.ts` builds them
 * from the map's tiles), so this is plain arithmetic, tested on its own.
 */

/** A line, as [longitude, latitude] points in order (a river's runs downstream). */
export type Line = [number, number][];

export interface RoadLine {
  coordinates: Line;
  quality: Exclude<RoadQuality, "None">;
}

export interface PlacePoint extends Point {
  size: Exclude<SettlementSize, "None">;
  name: string;
  capital: CapitalStatus;
}

export interface TerrainSources {
  /** The ground's height in metres, or null where there's no data. */
  elevation: (point: Point) => number | null;
  forest: (point: Point) => boolean;
  water: (point: Point) => boolean;
  places: PlacePoint[];
  roads: RoadLine[];
  /** Navigable rivers and canals: a waterway across the edges they cross, a river along them. */
  rivers: Line[];
}

export interface InferredTerrain {
  cells: InferredHexCell[];
  edges: InferredHexEdge[];
}

/** The rise within a hex, in metres, below which it's each relief (else Mountain). */
export const reliefLimits = { Flat: 50, LowHill: 150, HighHill: 400 } as const;

/** The share of a hex's samples that makes it forest, or water. */
const mostly = 0.5;

// The Earth radius hex-grid.ts uses.
const earthRadius = 6_371_008.8;

/** A point `east` and `north` metres from another. */
function offset(point: Point, east: number, north: number): Point {
  return {
    latitude: point.latitude + ((north / earthRadius) * 180) / Math.PI,
    longitude:
      point.longitude +
      ((east / (earthRadius * Math.cos((point.latitude * Math.PI) / 180))) * 180) / Math.PI,
  };
}

/**
 * 19 points spread over a hex: its centre, a ring of 6 half-way out, and a ring of 12 near its
 * edge (towards the corners and the sides' middles).
 */
export function samplePoints(grid: HexGrid, hex: Hex, size: number): Point[] {
  const centre = grid.centre(hex);
  const side = size / Math.sqrt(3);
  const points = [centre];
  for (let i = 0; i < 6; i++) {
    const angle = (Math.PI / 3) * i + Math.PI / 6;
    points.push(offset(centre, 0.5 * side * Math.cos(angle), 0.5 * side * Math.sin(angle)));
  }
  for (let i = 0; i < 12; i++) {
    const angle = (Math.PI / 6) * i;
    // Corners are `side` out, the sides' middles √3/2 of that: stay inside either.
    const reach = (i % 2 === 0 ? 0.85 : 0.75) * side;
    points.push(offset(centre, reach * Math.cos(angle), reach * Math.sin(angle)));
  }
  return points;
}

/** A hex's relief from its heights: how far they rise above the lowest. */
export function reliefOf(heights: number[]): Terrain {
  if (heights.length === 0) return "Flat";
  const rise = Math.max(...heights) - Math.min(...heights);
  return rise < reliefLimits.Flat
    ? "Flat"
    : rise < reliefLimits.LowHill
      ? "LowHill"
      : rise < reliefLimits.HighHill
        ? "HighHill"
        : "Mountain";
}

const placeRank = (place: PlacePoint) =>
  (place.size === "City" ? 10 : 0) +
  (place.capital === "Capital" ? 2 : place.capital === "Minor" ? 1 : 0);

/** The direction (index into `directions`) from a hex to its neighbour, or -1. */
function directionTo(from: Hex, to: Hex) {
  return directions.findIndex((d) => from.q + d.q === to.q && from.r + d.r === to.r);
}

/** A line's points, no more than `step` metres apart. */
export function densify(line: Line, step: number): Point[] {
  const points: Point[] = [];
  for (let i = 0; i < line.length; i++) {
    const [lng, lat] = line[i] ?? [0, 0];
    points.push({ longitude: lng, latitude: lat });
    if (i === line.length - 1) break;
    const next = line[i + 1] ?? [lng, lat];
    const east =
      (next[0] - lng) * Math.cos((lat * Math.PI) / 180) * ((earthRadius * Math.PI) / 180);
    const north = (next[1] - lat) * ((earthRadius * Math.PI) / 180);
    const steps = Math.ceil(Math.hypot(east, north) / step);
    for (let s = 1; s < steps; s++) {
      points.push({
        longitude: lng + ((next[0] - lng) * s) / steps,
        latitude: lat + ((next[1] - lat) * s) / steps,
      });
    }
  }
  return points;
}

/** The hexes a line passes through, in order, each once where it enters. */
export function hexesAlong(grid: HexGrid, line: Line, size: number): Hex[] {
  const hexes: Hex[] = [];
  for (const point of densify(line, size / 8)) {
    const hex = grid.hexAt(point);
    const last = hexes.at(-1);
    if (last?.q !== hex.q || last.r !== hex.r) hexes.push(hex);
  }
  return hexes;
}

/** The corner indices each side joins (the grid's corners: E, SE, SW, W, NW, NE). */
const sideByCorners: Record<string, Side> = {
  "0,1": "SE",
  "1,2": "S",
  "2,3": "SW",
  "3,4": "NW",
  "4,5": "N",
  "0,5": "NE",
};

// A corner's key: shared corners of neighbouring hexes round to the same one (0.1 m apart).
const cornerKey = ([lng, lat]: [number, number]) => `${lng.toFixed(6)},${lat.toFixed(6)}`;

interface CornerEdge {
  hex: Hex;
  side: Side;
  a: string;
  b: string;
}

/** The sides of these hexes and their neighbours, by the corners they join. */
function cornerGraph(grid: HexGrid, around: Hex[]) {
  const seen = new Set<string>();
  const edges: CornerEdge[] = [];
  for (const hex of around.flatMap((h) => [h, ...neighbours(h)])) {
    if (seen.has(hexKey(hex))) continue;
    seen.add(hexKey(hex));
    const corners = grid.corners(hex).map(cornerKey);
    for (const [pair, side] of Object.entries(sideByCorners)) {
      const [i, j] = pair.split(",").map(Number) as [number, number];
      edges.push({ hex, side, a: corners[i] ?? "", b: corners[j] ?? "" });
    }
  }
  return edges;
}

/** The fewest sides joining two corners (at most four), found among `edges`. */
function sidesBetween(edges: CornerEdge[], from: string, to: string): CornerEdge[] {
  const byCorner = new Map<string, CornerEdge[]>();
  for (const edge of edges) {
    for (const corner of [edge.a, edge.b]) {
      byCorner.set(corner, [...(byCorner.get(corner) ?? []), edge]);
    }
  }
  const previous = new Map<string, { corner: string; edge: CornerEdge }>();
  let frontier = [from];
  for (let depth = 0; depth < 4 && !previous.has(to); depth++) {
    const next: string[] = [];
    for (const corner of frontier) {
      for (const edge of byCorner.get(corner) ?? []) {
        const other = edge.a === corner ? edge.b : edge.a;
        if (other === from || previous.has(other)) continue;
        previous.set(other, { corner, edge });
        next.push(other);
      }
    }
    frontier = next;
  }
  const path: CornerEdge[] = [];
  for (let corner = to; corner !== from;) {
    const step = previous.get(corner);
    if (!step) return [];
    path.push(step.edge);
    corner = step.corner;
  }
  return path;
}

/**
 * The sides a river lies along: its line snapped to the nearest hex corners, and each corner
 * joined to the next along the hexes' sides, so the river is unbroken.
 */
export function sidesAlong(grid: HexGrid, line: Line, size: number): { hex: Hex; side: Side }[] {
  const corners: { key: string; hex: Hex }[] = [];
  for (const point of densify(line, size / 6)) {
    const hex = grid.hexAt(point);
    const nearest = grid
      .corners(hex)
      .map((corner) => ({
        corner,
        distance: Math.hypot(
          (corner[0] - point.longitude) * Math.cos((point.latitude * Math.PI) / 180),
          corner[1] - point.latitude,
        ),
      }))
      .reduce((best, c) => (c.distance < best.distance ? c : best));
    const key = cornerKey(nearest.corner);
    if (corners.at(-1)?.key !== key) corners.push({ key, hex });
  }
  const found: { hex: Hex; side: Side }[] = [];
  for (let i = 1; i < corners.length; i++) {
    const from = corners[i - 1];
    const to = corners[i];
    const graph = cornerGraph(grid, [from.hex, to.hex]);
    for (const edge of sidesBetween(graph, from.key, to.key)) {
      found.push({ hex: edge.hex, side: edge.side });
    }
  }
  return found;
}

interface EdgeState {
  q: number;
  r: number;
  side: InferredHexEdge["side"];
  road: RoadQuality;
  river: boolean;
  waterway: Waterway;
}

/** Waits a moment, so the page can draw (and show progress) during a long run. */
const breathe = () =>
  new Promise<void>((resolve) => {
    setTimeout(resolve, 0);
  });

/**
 * The grid's terrain from the map's data: each hex's relief (the rise across it), forest and
 * water (half its samples or more), and largest town or city; each edge's best road across it,
 * a navigable river's course across it (the way the river flows), a river along it where one
 * lies between hexes, and a bridge where a road crosses such a river. Only what has something
 * on it is returned, inside the grid (or on its border, for edges).
 */
export async function inferTerrain(
  grid: HexGrid,
  size: number,
  sources: TerrainSources,
  onProgress?: (done: number, total: number) => void,
): Promise<InferredTerrain> {
  const hexes = grid.hexes();
  const placesByHex = new Map<string, PlacePoint>();
  for (const place of sources.places) {
    const key = hexKey(grid.hexAt(place));
    const current = placesByHex.get(key);
    if (!current || placeRank(place) > placeRank(current)) placesByHex.set(key, place);
  }

  const cells: InferredHexCell[] = [];
  for (let i = 0; i < hexes.length; i++) {
    const hex = hexes[i] ?? { q: 0, r: 0 };
    const samples = samplePoints(grid, hex, size);
    const heights = samples
      .map((point) => sources.elevation(point))
      .filter((height): height is number => height !== null);
    const water = samples.filter((point) => sources.water(point)).length / samples.length;
    const forest = samples.filter((point) => sources.forest(point)).length / samples.length;
    const place = placesByHex.get(hexKey(hex));
    const cell: InferredHexCell = {
      q: hex.q,
      r: hex.r,
      terrain: water >= mostly ? "Water" : reliefOf(heights),
      forest: water < mostly && forest >= mostly,
      settlement: place
        ? { ...noSettlement, size: place.size, capital: place.capital, name: place.name }
        : noSettlement,
    };
    if (cell.terrain !== "Flat" || cell.forest || place) cells.push(cell);
    if (i % 250 === 249) {
      onProgress?.(i + 1, hexes.length);
      await breathe();
    }
  }

  onProgress?.(hexes.length, hexes.length);

  const edges = new Map<string, EdgeState>();
  const edgeAt = (hex: Hex, side: Side) => {
    const stored = storedEdge(hex, side);
    const key = edgeKey(stored);
    let edge = edges.get(key);
    if (!edge) {
      edge = {
        q: stored.q,
        r: stored.r,
        side: stored.side,
        road: "None",
        river: false,
        waterway: "None",
      };
      edges.set(key, edge);
    }
    return { edge, flipped: stored.flipped };
  };
  const sides = ["N", "NE", "SE", "S", "SW", "NW"] as const;

  for (const road of sources.roads) {
    const along = hexesAlong(grid, road.coordinates, size);
    for (let i = 1; i < along.length; i++) {
      const from = along[i - 1] ?? { q: 0, r: 0 };
      const direction = directionTo(from, along[i] ?? from);
      if (direction < 0) continue;
      const { edge } = edgeAt(from, sides[direction] ?? "N");
      if (edge.road !== "Good") edge.road = road.quality;
    }
  }
  for (const river of sources.rivers) {
    const along = hexesAlong(grid, river, size);
    for (let i = 1; i < along.length; i++) {
      const from = along[i - 1] ?? { q: 0, r: 0 };
      const direction = directionTo(from, along[i] ?? from);
      if (direction < 0) continue;
      const { edge, flipped } = edgeAt(from, sides[direction] ?? "N");
      // Out of `from` into the next hex, as the edge's own hex sees it.
      if (edge.waterway === "None") edge.waterway = flipped ? "In" : "Out";
    }
    for (const { hex, side } of sidesAlong(grid, river, size)) {
      edgeAt(hex, side).edge.river = true;
    }
  }

  const touches = (edge: EdgeState) => {
    const d = directions[["N", "NE", "SE"].indexOf(edge.side)] ?? { q: 0, r: 0 };
    return grid.contains(edge) || grid.contains({ q: edge.q + d.q, r: edge.r + d.r });
  };
  return {
    cells,
    edges: [...edges.values()]
      .filter(
        (edge) => touches(edge) && (edge.road !== "None" || edge.river || edge.waterway !== "None"),
      )
      .map((edge) => ({ ...edge, bridge: edge.river && edge.road !== "None" })),
  };
}
