import type { MapBounds } from "@/api/generated/model";
import type { Point } from "@/features/maps/geo";

/**
 * The campaign's hex grid (decision 0014): flat-topped hexes `size` metres across the flats, laid
 * over the area in a local flat projection centred on it (east = R·cos φ₀·Δλ, south = R·Δφ),
 * with hex (0, 0) centred on the area's middle. Axial coordinates: q east, r south-east. The grid
 * is every hex whose centre is inside the bounds. The API's HexGrid.cs does the same arithmetic;
 * both are tested against testdata/hex-grid.json.
 */

/** A hex, by its axial coordinates. */
export interface Hex {
  q: number;
  r: number;
}

// The mean Earth radius, as the API and geo.ts use.
const earthRadius = 6_371_008.8;
const radians = (degrees: number) => (degrees * Math.PI) / 180;
const degrees = (value: number) => (value * 180) / Math.PI;
// Half up, as the API and its migrations round (C#'s and SQL's own rounding differ on halves).
const round = (value: number) => Math.floor(value + 0.5);

/** The six neighbours' offsets, clockwise from north: N, NE, SE, S, SW, NW. */
export const directions: readonly Hex[] = [
  { q: 0, r: -1 },
  { q: 1, r: -1 },
  { q: 1, r: 0 },
  { q: 0, r: 1 },
  { q: -1, r: 1 },
  { q: -1, r: 0 },
];

export const neighbours = ({ q, r }: Hex): Hex[] =>
  directions.map((d) => ({ q: q + d.q, r: r + d.r }));

/** How many steps apart two hexes are. */
export const hexDistance = (a: Hex, b: Hex) =>
  (Math.abs(a.q - b.q) + Math.abs(a.q + a.r - b.q - b.r) + Math.abs(a.r - b.r)) / 2;

/** A hex as a string, for keys and sets. */
export const hexKey = ({ q, r }: Hex) => `${String(q)},${String(r)}`;

/** A hex in words, for people: "Hex (3, −2)". */
export const hexName = ({ q, r }: Hex) => `Hex (${String(q)}, ${String(r)})`.replace(/-/g, "−");

export interface HexGrid {
  /** The hex a point falls in (whether or not it's in the grid). */
  hexAt: (point: Point) => Hex;
  /** A hex's centre. */
  centre: (hex: Hex) => Point;
  /** A hex's six corners, as [longitude, latitude] (GeoJSON's order), clockwise from east. */
  corners: (hex: Hex) => [number, number][];
  /** Whether a hex is in the grid (its centre inside the bounds). */
  contains: (hex: Hex) => boolean;
  /** Every hex in the grid. */
  hexes: () => Hex[];
}

/** The most hexes drawn: more (an area the size of a country, at 3 miles) would stall the page. */
export const maxDrawnHexes = 30_000;

/** About how many hexes of `size` metres an area holds, without laying them out. */
export function hexCount(bounds: MapBounds, size: number) {
  const lat0 = radians((bounds.south + bounds.north) / 2);
  const width = earthRadius * Math.cos(lat0) * radians(bounds.east - bounds.west);
  const height = earthRadius * radians(bounds.north - bounds.south);
  return Math.round((width * height) / ((Math.sqrt(3) / 2) * size * size));
}

/** An area's size in hexes: its columns, and the hexes down one. */
export interface HexDimensions {
  across: number;
  down: number;
}

/**
 * About how many hexes of `size` metres an area is across and down, without laying them out:
 * flat-topped columns are 1.5 corner-to-centre distances apart, rows a hex's height (`size`),
 * and a hex is in the grid when its centre is, at either edge too.
 */
export function hexDimensions(bounds: MapBounds, size: number): HexDimensions {
  const lat0 = radians((bounds.south + bounds.north) / 2);
  const width = earthRadius * Math.cos(lat0) * radians(bounds.east - bounds.west);
  const height = earthRadius * radians(bounds.north - bounds.south);
  return {
    across: Math.floor(width / ((1.5 * size) / Math.sqrt(3))) + 1,
    down: Math.floor(height / size) + 1,
  };
}

/** A laid-out grid's size in hexes: how many columns, and the most hexes in one. */
export function gridDimensions(hexes: readonly Hex[]): HexDimensions {
  const columns = new Map<number, number>();
  for (const { q } of hexes) columns.set(q, (columns.get(q) ?? 0) + 1);
  return { across: columns.size, down: Math.max(0, ...columns.values()) };
}

export function hexGrid(bounds: MapBounds, size: number): HexGrid {
  const lat0 = (bounds.south + bounds.north) / 2;
  const lon0 = (bounds.west + bounds.east) / 2;
  const k = earthRadius * Math.cos(radians(lat0));
  // The corner-to-centre distance.
  const side = size / Math.sqrt(3);

  const project = ({ latitude, longitude }: Point) => ({
    x: k * radians(longitude - lon0),
    y: earthRadius * radians(lat0 - latitude),
  });
  const unproject = (x: number, y: number): Point => ({
    latitude: lat0 - degrees(y / earthRadius),
    longitude: lon0 + degrees(x / k),
  });
  const centreXY = ({ q, r }: Hex) => ({ x: 1.5 * side * q, y: size * (r + q / 2) });
  const centre = (hex: Hex) => {
    const { x, y } = centreXY(hex);
    return unproject(x, y);
  };
  const contains = (hex: Hex) => {
    const { latitude, longitude } = centre(hex);
    return (
      latitude >= bounds.south &&
      latitude <= bounds.north &&
      longitude >= bounds.west &&
      longitude <= bounds.east
    );
  };

  return {
    hexAt(point) {
      const { x, y } = project(point);
      const qf = ((2 / 3) * x) / side;
      const rf = ((-1 / 3) * x + (Math.sqrt(3) / 3) * y) / side;
      const sf = -qf - rf;
      let q = round(qf);
      let r = round(rf);
      const s = round(sf);
      const dq = Math.abs(q - qf);
      const dr = Math.abs(r - rf);
      const ds = Math.abs(s - sf);
      if (dq > dr && dq > ds) q = -r - s;
      else if (dr >= ds) r = -q - s;
      return { q: q === 0 ? 0 : q, r: r === 0 ? 0 : r };
    },
    centre,
    corners(hex) {
      const { x, y } = centreXY(hex);
      return Array.from({ length: 6 }, (_, i) => {
        const angle = (Math.PI / 3) * i;
        const corner = unproject(x + side * Math.cos(angle), y + side * Math.sin(angle));
        return [corner.longitude, corner.latitude];
      });
    },
    contains,
    hexes() {
      const west = project({ latitude: lat0, longitude: bounds.west }).x;
      const east = project({ latitude: lat0, longitude: bounds.east }).x;
      const north = project({ latitude: bounds.north, longitude: lon0 }).y;
      const south = project({ latitude: bounds.south, longitude: lon0 }).y;
      const found: Hex[] = [];
      for (
        let q = Math.floor(west / (1.5 * side)) - 1;
        q <= Math.ceil(east / (1.5 * side)) + 1;
        q++
      ) {
        for (
          let r = Math.floor(north / size - q / 2) - 1;
          r <= Math.ceil(south / size - q / 2) + 1;
          r++
        ) {
          if (contains({ q, r })) found.push({ q, r });
        }
      }
      return found;
    },
  };
}
