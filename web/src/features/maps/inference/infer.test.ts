import { describe, expect, it } from "vitest";
import type { InferredHexEdge } from "@/api/generated/model";
import type { Point } from "@/features/maps/geo";
import { hexGrid, hexKey, type Hex } from "@/features/maps/hex-grid";
import {
  inferTerrain,
  reliefOf,
  samplePoints,
  type Line,
  type TerrainSources,
} from "@/features/maps/inference/infer";
import { sideCorners } from "@/features/maps/terrain";

const size = 4828;
const grid = hexGrid({ west: 4.2, south: 50.6, east: 4.6, north: 50.8 }, size);
const at = (hex: Hex, point: Point) => hexKey(grid.hexAt(point)) === hexKey(hex);
const centre = (hex: Hex): [number, number] => {
  const { longitude, latitude } = grid.centre(hex);
  return [longitude, latitude];
};

const nothing: TerrainSources = {
  elevation: () => 100,
  forest: () => false,
  water: () => false,
  places: [],
  roads: [],
  rivers: [],
};

describe("samplePoints", () => {
  it("spreads 19 points, all inside the hex", () => {
    const points = samplePoints(grid, { q: 1, r: -1 }, size);
    expect(points).toHaveLength(19);
    for (const point of points) expect(grid.hexAt(point)).toEqual({ q: 1, r: -1 });
  });
});

describe("reliefOf", () => {
  it("goes by how far the ground rises across the hex", () => {
    expect(reliefOf([])).toBe("Flat");
    expect(reliefOf([300, 349])).toBe("Flat");
    expect(reliefOf([300, 350])).toBe("LowHill");
    expect(reliefOf([300, 450])).toBe("HighHill");
    expect(reliefOf([300, 700])).toBe("Mountain");
  });
});

describe("inferTerrain", () => {
  it("finds each hex's relief, forest, water and largest place", async () => {
    const sources: TerrainSources = {
      ...nothing,
      // Hex (0, 0) rises 200 m from west to east; every other hex is level.
      elevation: (point) =>
        at({ q: 0, r: 0 }, point)
          ? point.longitude > grid.centre({ q: 0, r: 0 }).longitude
            ? 300
            : 100
          : 100,
      forest: (point) => at({ q: 1, r: 0 }, point) || at({ q: -1, r: 0 }, point),
      water: (point) => at({ q: -1, r: 0 }, point),
      places: [
        { ...grid.centre({ q: 0, r: 1 }), size: "Town", name: "Plancenoit", capital: "Minor" },
        { ...grid.centre({ q: 2, r: 0 }), size: "Town", name: "Wavre", capital: "None" },
        { ...grid.centre({ q: 2, r: 0 }), size: "City", name: "Ottignies", capital: "None" },
      ],
    };

    const { cells } = await inferTerrain(grid, size, sources);

    const byHex = new Map(cells.map((cell) => [hexKey(cell), cell]));
    expect(byHex.get("0,0")).toMatchObject({ terrain: "HighHill", forest: false });
    expect(byHex.get("1,0")).toMatchObject({ terrain: "Flat", forest: true });
    // Water wins over the forest drawn under it.
    expect(byHex.get("-1,0")).toMatchObject({ terrain: "Water", forest: false });
    expect(byHex.get("0,1")?.settlement).toEqual({
      size: "Town",
      walled: false,
      fortress: false,
      capital: "Minor",
      name: "Plancenoit",
    });
    expect(byHex.get("2,0")?.settlement.name).toBe("Ottignies");
    // Only hexes with something on them.
    expect(cells).toHaveLength(5);
  });

  it("marks the edges a road crosses, with the best road", async () => {
    const { edges } = await inferTerrain(grid, size, {
      ...nothing,
      roads: [
        { coordinates: [centre({ q: 0, r: 0 }), centre({ q: 0, r: -2 })], quality: "Poor" },
        { coordinates: [centre({ q: 0, r: 0 }), centre({ q: 0, r: -1 })], quality: "Good" },
      ],
    });

    expect(edges).toEqual([
      { q: 0, r: 0, side: "N", road: "Good", river: false, waterway: "None", bridge: false },
      { q: 0, r: -1, side: "N", road: "Poor", river: false, waterway: "None", bridge: false },
    ]);
  });

  it("follows a river's course across edges the way it flows", async () => {
    // Downstream: south to north, then on to the south-west.
    const river: Line = [centre({ q: 1, r: 1 }), centre({ q: 1, r: 0 }), centre({ q: 0, r: 1 })];

    const { edges } = await inferTerrain(grid, size, { ...nothing, rivers: [river] });

    const waterways = edges.filter((edge) => edge.waterway !== "None");
    expect(waterways.map(({ q, r, side, waterway }) => ({ q, r, side, waterway }))).toEqual([
      // Out of (1, 1) north into (1, 0).
      { q: 1, r: 1, side: "N", waterway: "Out" },
      // Out of (1, 0) south-west into (0, 1): stored as (0, 1)'s NE, flowing in.
      { q: 0, r: 1, side: "NE", waterway: "In" },
    ]);
  });

  it("puts a river along an unbroken line of sides near it, bridged where a road crosses", async () => {
    // A river running north through the middle of column 1, and a road crossing it east-west.
    const river: Line = [centre({ q: 1, r: 2 }), centre({ q: 1, r: -2 })];
    const road = {
      coordinates: [centre({ q: -1, r: 0 }), centre({ q: 3, r: -2 })],
      quality: "Good" as const,
    };

    const { edges } = await inferTerrain(grid, size, {
      ...nothing,
      rivers: [river],
      roads: [road],
    });

    const along = edges.filter((edge) => edge.river);
    expect(along.length).toBeGreaterThanOrEqual(4);
    // Each lies near the river's line (within a hex's width of it).
    const riverLongitude = grid.centre({ q: 1, r: 0 }).longitude;
    const cornersOf = (edge: InferredHexEdge) => sideCorners(grid, edge, edge.side);
    for (const edge of along) {
      for (const [longitude] of cornersOf(edge)) {
        expect(Math.abs(longitude - riverLongitude)).toBeLessThan(0.07);
      }
    }
    // Unbroken: the sides join corner to corner into one line.
    const key = ([x, y]: [number, number]) => `${x.toFixed(6)},${y.toFixed(6)}`;
    const links = new Map<string, string[]>();
    for (const edge of along) {
      const [a, b] = cornersOf(edge).map(key) as [string, string];
      links.set(a, [...(links.get(a) ?? []), b]);
      links.set(b, [...(links.get(b) ?? []), a]);
    }
    const start = [...links.keys()][0] ?? "";
    const reached = new Set([start]);
    const queue = [start];
    while (queue.length > 0) {
      for (const next of links.get(queue.shift() ?? "") ?? []) {
        if (!reached.has(next)) {
          reached.add(next);
          queue.push(next);
        }
      }
    }
    expect(reached.size).toBe(links.size);
    // The road crosses it on a bridge; every bridge has a road and a river.
    const bridges = edges.filter((edge) => edge.bridge);
    expect(bridges.length).toBeGreaterThanOrEqual(1);
    for (const bridge of bridges) expect(bridge).toMatchObject({ river: true, road: "Good" });
  });

  it("leaves out edges off the grid, and reports its progress", async () => {
    const progress: number[] = [];
    const far = grid.hexes().reduce((a, b) => (b.r > a.r ? b : a));
    const beyond = { q: far.q, r: far.r + 3 };

    const { edges } = await inferTerrain(
      grid,
      size,
      { ...nothing, roads: [{ coordinates: [centre(far), centre(beyond)], quality: "Good" }] },
      (done) => progress.push(done),
    );

    // The road's first edge is on the border (its hex is in the grid); the others are off it.
    expect(edges).toHaveLength(1);
    expect(progress.length).toBeGreaterThan(0);
  });
});
