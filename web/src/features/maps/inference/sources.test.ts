import { describe, expect, it } from "vitest";
import {
  buildSources,
  inPolygon,
  terrariumHeight,
  tileAt,
  type ElevationTileData,
  type VectorTileData,
} from "@/features/maps/inference/sources";
import { tilesCovering, zoomFor } from "@/features/maps/inference/tiles";

// Brussels, and the zoom-10 tile it's in.
const brussels = { longitude: 4.35, latitude: 50.85 };
const { x, y } = tileAt(brussels, 10);

const square = (west: number, south: number, east: number, north: number) => [
  [west, south],
  [east, south],
  [east, north],
  [west, north],
  [west, south],
];

describe("tileAt", () => {
  it("finds a point's Web Mercator tile, and where in it", () => {
    expect(tileAt({ longitude: 0, latitude: 0 }, 1)).toEqual({ x: 1, y: 1, fx: 0, fy: 0 });
    expect(tileAt({ longitude: -180, latitude: 85 }, 3)).toMatchObject({ x: 0, y: 0 });
    // Brussels is in the zoom-10 tile 524/343.
    expect({ x, y }).toEqual({ x: 524, y: 343 });
  });
});

describe("terrariumHeight", () => {
  it("reads Terrarium's encoding", () => {
    expect(terrariumHeight(128, 0, 0)).toBe(0);
    expect(terrariumHeight(128, 100, 128)).toBe(100.5);
    expect(terrariumHeight(127, 255, 0)).toBe(-1);
  });
});

describe("inPolygon", () => {
  it("is inside the outer ring and outside its holes", () => {
    const polygon = {
      bbox: [0, 0, 10, 10] as [number, number, number, number],
      rings: [square(0, 0, 10, 10), square(4, 4, 6, 6)] as [number, number][][],
    };
    expect(inPolygon([1, 1], polygon)).toBe(true);
    expect(inPolygon([5, 5], polygon)).toBe(false);
    expect(inPolygon([11, 5], polygon)).toBe(false);
  });
});

describe("tiles to read", () => {
  it("reads 3-mile hexes' data at zoom 10 and their heights at zoom 8", () => {
    const area = { west: 4.2, south: 50.6, east: 4.6, north: 50.8 };
    expect(zoomFor(area, 5 * 4828, [9, 14])).toBe(10);
    expect(zoomFor(area, (512 * 4828) / 25, [5, 12])).toBe(8);
  });

  it("reads a lower zoom rather than too many tiles", () => {
    const europe = { west: -10, south: 36, east: 30, north: 60 };
    const z = zoomFor(europe, 5 * 500, [9, 14]);
    expect(tilesCovering(europe, z).length).toBeLessThanOrEqual(400);
  });
});

describe("buildSources", () => {
  const tile: VectorTileData = {
    x,
    y,
    z: 10,
    features: [
      {
        layer: "landcover",
        properties: { class: "wood" },
        geometry: { type: "Polygon", coordinates: [square(4.34, 50.84, 4.36, 50.86)] },
      },
      {
        layer: "landcover",
        properties: { class: "grass" },
        geometry: { type: "Polygon", coordinates: [square(4.3, 50.8, 4.4, 50.9)] },
      },
      {
        layer: "water",
        properties: { class: "lake" },
        geometry: {
          type: "MultiPolygon",
          coordinates: [[square(4.3, 50.84, 4.32, 50.86)]],
        },
      },
      ...(["trunk", "primary", "secondary", "tertiary", "motorway"] as const).map((road) => ({
        layer: "transportation",
        properties: { class: road },
        geometry: {
          type: "LineString" as const,
          coordinates: [
            [4.3, 50.85],
            [4.4, 50.85],
          ],
        },
      })),
      ...(["river", "canal", "stream"] as const).map((waterway) => ({
        layer: "waterway",
        properties: { class: waterway },
        geometry: {
          type: "MultiLineString" as const,
          coordinates: [
            [
              [4.3, 50.8],
              [4.3, 50.9],
            ],
          ],
        },
      })),
      {
        layer: "place",
        properties: { class: "city", name: "Brussel", "name:fr": "Bruxelles", capital: 2 },
        geometry: { type: "Point", coordinates: [4.35, 50.85] },
      },
      {
        layer: "place",
        properties: { class: "town", name: "Waterloo", capital: 4 },
        geometry: { type: "Point", coordinates: [4.4, 50.72] },
      },
      {
        layer: "place",
        properties: { class: "village", name: "Plancenoit" },
        geometry: { type: "Point", coordinates: [4.42, 50.66] },
      },
    ],
  };
  // The same town again from a neighbouring tile's buffer.
  const neighbour: VectorTileData = {
    x: x + 1,
    y,
    z: 10,
    features: [tile.features.find((f) => f.properties.name === "Waterloo") ?? tile.features[0]],
  };

  it("answers forest and water from the tile the point is in", () => {
    const sources = buildSources([tile], [], "en");
    expect(sources.forest(brussels)).toBe(true);
    expect(sources.forest({ longitude: 4.38, latitude: 50.85 })).toBe(false);
    expect(sources.water({ longitude: 4.31, latitude: 50.85 })).toBe(true);
    expect(sources.water(brussels)).toBe(false);
  });

  it("keeps the main roads by quality, and rivers and canals, not streams", () => {
    const sources = buildSources([tile], [], "en");
    expect(sources.roads.map((road) => road.quality)).toEqual(["Good", "Good", "Poor"]);
    expect(sources.rivers).toHaveLength(2);
  });

  it("keeps cities and towns once each, named in the language, with their capital status", () => {
    expect(buildSources([tile, neighbour], [], "fr").places).toEqual([
      { longitude: 4.35, latitude: 50.85, size: "City", name: "Bruxelles", capital: "Capital" },
      { longitude: 4.4, latitude: 50.72, size: "Town", name: "Waterloo", capital: "Minor" },
    ]);
    expect(buildSources([tile], [], "local").places[0]?.name).toBe("Brussel");
  });

  it("reads heights from the elevation tile's pixels", () => {
    const { x: ex, y: ey, fx, fy } = tileAt(brussels, 8);
    // A 2 × 2 tile: 0 m, 100 m / 200 m, 300 m.
    const heights = [0, 100, 200, 300];
    const pixels = new Uint8ClampedArray(
      heights.flatMap((h) => [Math.floor((h + 32768) / 256), (h + 32768) % 256, 0, 255]),
    );
    const elevation: ElevationTileData = { x: ex, y: ey, z: 8, width: 2, height: 2, pixels };

    const sources = buildSources([], [elevation], "en");

    expect(sources.elevation(brussels)).toBe(heights[(fy < 0.5 ? 0 : 2) + (fx < 0.5 ? 0 : 1)]);
    expect(sources.elevation({ longitude: -70, latitude: 40 })).toBeNull();
  });
});
