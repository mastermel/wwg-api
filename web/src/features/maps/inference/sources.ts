import type { CapitalStatus } from "@/api/generated/model";
import type { Point } from "@/features/maps/geo";
import type { Line, PlacePoint, RoadLine, TerrainSources } from "@/features/maps/inference/infer";

/**
 * The map's data, read for inference: OpenMapTiles vector tiles (land cover, water, waterways,
 * roads, places) and Terrarium elevation tiles (Mapterhorn), decoded by `tiles.ts`, turned into
 * the questions `inferTerrain` asks. Plain arithmetic, tested on its own.
 */

/** A vector tile's features, as GeoJSON in degrees. */
export interface VectorTileData {
  x: number;
  y: number;
  z: number;
  features: { layer: string; properties: Record<string, unknown>; geometry: GeoJSON.Geometry }[];
}

/** An elevation tile's pixels, RGBA, in Terrarium's encoding. */
export interface ElevationTileData {
  x: number;
  y: number;
  z: number;
  width: number;
  height: number;
  pixels: Uint8ClampedArray;
}

/** Where a point is in the Web Mercator tiles of zoom `z`: the tile, and how far across it. */
export function tileAt({ longitude, latitude }: Point, z: number) {
  const n = 2 ** z;
  const x = ((longitude + 180) / 360) * n;
  const phi = (latitude * Math.PI) / 180;
  const y = ((1 - Math.log(Math.tan(phi) + 1 / Math.cos(phi)) / Math.PI) / 2) * n;
  return { x: Math.floor(x), y: Math.floor(y), fx: x - Math.floor(x), fy: y - Math.floor(y) };
}

/** Terrarium's height, in metres, from a pixel's red, green and blue. */
export const terrariumHeight = (red: number, green: number, blue: number) =>
  red * 256 + green + blue / 256 - 32768;

type Ring = [number, number][];

interface Polygon {
  bbox: [number, number, number, number];
  rings: Ring[];
}

/** Whether a point is inside a ring (even-odd). */
function inRing([x, y]: [number, number], ring: Ring) {
  let inside = false;
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const [xi, yi] = ring[i] ?? [0, 0];
    const [xj, yj] = ring[j] ?? [0, 0];
    if (yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
  }
  return inside;
}

/** Inside a polygon: in its outer ring, and in none of its holes. */
export function inPolygon(point: [number, number], polygon: Polygon) {
  const [x, y] = point;
  const [west, south, east, north] = polygon.bbox;
  if (x < west || x > east || y < south || y > north) return false;
  const [outer, ...holes] = polygon.rings;
  return (
    polygon.rings.length > 0 && inRing(point, outer) && !holes.some((hole) => inRing(point, hole))
  );
}

function toPolygons(geometry: GeoJSON.Geometry): Polygon[] {
  const polygons =
    geometry.type === "Polygon"
      ? [geometry.coordinates]
      : geometry.type === "MultiPolygon"
        ? geometry.coordinates
        : [];
  return polygons.map((rings) => {
    const outer = (rings[0] ?? []) as Ring;
    const xs = outer.map(([x]) => x);
    const ys = outer.map(([, y]) => y);
    return {
      bbox: [Math.min(...xs), Math.min(...ys), Math.max(...xs), Math.max(...ys)],
      rings: rings as Ring[],
    };
  });
}

function toLines(geometry: GeoJSON.Geometry): Line[] {
  if (geometry.type === "LineString") return [geometry.coordinates as Line];
  if (geometry.type === "MultiLineString") return geometry.coordinates as Line[];
  return [];
}

const text = (value: unknown) => (typeof value === "string" && value ? value : undefined);

/** OpenMapTiles' capital: the admin level it's the capital of (2, a country's). */
function capitalOf(value: unknown): CapitalStatus {
  return value === 2 ? "Capital" : value === 3 || value === 4 ? "Minor" : "None";
}

const roadQuality: Record<string, RoadLine["quality"] | undefined> = {
  trunk: "Good",
  primary: "Good",
  secondary: "Poor",
};

/**
 * What inference asks of the map's tiles, in the campaign's label language ("local": each
 * place's own name). Forest is land cover's "wood"; water, the water layer's areas; roads, the
 * trunk and primary (good) and secondary (poor) roads, as the map draws; rivers, the waterways
 * that are rivers or canals (streams are the hexes' detail, not the map's); places, cities and
 * towns (villages are detail too).
 */
export function buildSources(
  vectorTiles: VectorTileData[],
  elevationTiles: ElevationTileData[],
  language: string,
): TerrainSources {
  const tileKey = (x: number, y: number) => `${String(x)}/${String(y)}`;
  const forests = new Map<string, Polygon[]>();
  const waters = new Map<string, Polygon[]>();
  const roads: RoadLine[] = [];
  const rivers: Line[] = [];
  const places = new Map<string, PlacePoint>();
  const vectorZoom = vectorTiles[0]?.z ?? 0;

  for (const tile of vectorTiles) {
    const key = tileKey(tile.x, tile.y);
    for (const { layer, properties, geometry } of tile.features) {
      if (layer === "landcover" && properties.class === "wood") {
        forests.set(key, [...(forests.get(key) ?? []), ...toPolygons(geometry)]);
      } else if (layer === "water") {
        waters.set(key, [...(waters.get(key) ?? []), ...toPolygons(geometry)]);
      } else if (layer === "transportation") {
        const quality = roadQuality[String(properties.class)];
        if (quality)
          roads.push(...toLines(geometry).map((coordinates) => ({ coordinates, quality })));
      } else if (
        layer === "waterway" &&
        (properties.class === "river" || properties.class === "canal")
      ) {
        rivers.push(...toLines(geometry));
      } else if (
        layer === "place" &&
        (properties.class === "city" || properties.class === "town") &&
        geometry.type === "Point"
      ) {
        const [longitude, latitude] = geometry.coordinates as [number, number];
        const name =
          (language === "local" ? undefined : text(properties[`name:${language}`])) ??
          text(properties.name) ??
          "";
        // A place near a tile's edge is in its neighbours' buffers too: keep one.
        places.set(`${name}@${longitude.toFixed(3)},${latitude.toFixed(3)}`, {
          longitude,
          latitude,
          size: properties.class === "city" ? "City" : "Town",
          name,
          capital: capitalOf(properties.capital),
        });
      }
    }
  }

  const within = (index: Map<string, Polygon[]>) => (point: Point) => {
    const { x, y } = tileAt(point, vectorZoom);
    const at: [number, number] = [point.longitude, point.latitude];
    return (index.get(tileKey(x, y)) ?? []).some((polygon) => inPolygon(at, polygon));
  };

  const elevationZoom = elevationTiles[0]?.z ?? 0;
  const elevation = new Map(elevationTiles.map((tile) => [tileKey(tile.x, tile.y), tile]));

  return {
    elevation(point) {
      const { x, y, fx, fy } = tileAt(point, elevationZoom);
      const tile = elevation.get(tileKey(x, y));
      if (!tile) return null;
      const px = Math.min(tile.width - 1, Math.floor(fx * tile.width));
      const py = Math.min(tile.height - 1, Math.floor(fy * tile.height));
      const i = (py * tile.width + px) * 4;
      return terrariumHeight(tile.pixels[i] ?? 0, tile.pixels[i + 1] ?? 0, tile.pixels[i + 2] ?? 0);
    },
    forest: within(forests),
    water: within(waters),
    places: [...places.values()],
    roads,
    rivers,
  };
}
