import { describe, expect, it } from "vitest";
import type { MapBounds } from "@/api/generated/model";
import { hexGrid } from "@/features/maps/hex-grid";
import { fillingView, playableOutline, viewLimits } from "@/features/maps/playable-area";

// About 30 km by 20 km around Waterloo.
const area: MapBounds = { west: 4.2, south: 50.6, east: 4.62, north: 50.78 };
const hexSize = 4828;

const mercatorY = (latitude: number) =>
  Math.log(Math.tan(Math.PI / 4 + (latitude * Math.PI) / 360));
const shape = (b: MapBounds) =>
  ((b.east - b.west) * Math.PI) / 180 / (mercatorY(b.north) - mercatorY(b.south));

/** Whether a point is inside a ring (ray casting). */
function inside([x, y]: [number, number], ring: [number, number][]) {
  let found = false;
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const [xi, yi] = ring[i] ?? [0, 0];
    const [xj, yj] = ring[j] ?? [0, 0];
    if (yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) found = !found;
  }
  return found;
}

describe("viewLimits", () => {
  it("takes in the area and a margin, widened to the map's shape either way", () => {
    for (const aspect of [0.5, 1, 3]) {
      const limits = viewLimits(area, aspect, hexSize);
      expect(limits.west).toBeLessThan(area.west);
      expect(limits.east).toBeGreaterThan(area.east);
      expect(limits.south).toBeLessThan(area.south);
      expect(limits.north).toBeGreaterThan(area.north);
      expect(shape(limits)).toBeCloseTo(aspect, 6);
      // Centred on the area.
      expect((limits.west + limits.east) / 2).toBeCloseTo((area.west + area.east) / 2, 9);
    }
  });

  it("keeps at least a hex around a small area", () => {
    const small = { west: 4.4, south: 50.7, east: 4.41, north: 50.71 };
    const limits = viewLimits(small, 1, hexSize);
    // A hex is about 0.068 degrees of longitude here.
    expect(small.west - limits.west).toBeGreaterThan(0.06);
  });

  it("stops at the world's edge", () => {
    const limits = viewLimits({ west: -179, south: -80, east: 179, north: 80 }, 1);
    expect(limits).toEqual({ west: -180, south: -85.05, east: 180, north: 85.05 });
  });
});

describe("fillingView", () => {
  it("is the area's middle, cut to the map's shape", () => {
    const tall = fillingView(area, 0.5);
    expect(tall.south).toBeCloseTo(area.south, 9);
    expect(tall.north).toBeCloseTo(area.north, 9);
    expect(tall.west).toBeGreaterThan(area.west);
    expect(shape(tall)).toBeCloseTo(0.5, 6);

    const wide = fillingView(area, 4);
    expect(wide.west).toBeCloseTo(area.west, 9);
    expect(wide.south).toBeGreaterThan(area.south);
    expect(shape(wide)).toBeCloseTo(4, 6);
  });
});

describe("playableOutline", () => {
  it("is the area's rectangle without a grid", () => {
    expect(playableOutline(area, null)).toEqual([
      [
        [4.2, 50.78],
        [4.62, 50.78],
        [4.62, 50.6],
        [4.2, 50.6],
        [4.2, 50.78],
      ],
    ]);
  });

  it("follows the grid's outer edge, one closed ring around every hex and nothing more", () => {
    const rings = playableOutline(area, hexSize);
    const grid = hexGrid(area, hexSize);

    expect(rings).toHaveLength(1);
    const ring = rings[0] ?? [];
    expect(ring[0]).toEqual(ring.at(-1));
    for (const hex of grid.hexes()) {
      const { longitude, latitude } = grid.centre(hex);
      expect(inside([longitude, latitude], ring)).toBe(true);
    }
    // The hexes just beyond the grid are outside it.
    const beyond = [
      { q: -20, r: 0 },
      { q: 0, r: -20 },
      { q: 20, r: 0 },
      { q: 0, r: 20 },
    ].filter((hex) => !grid.contains(hex));
    expect(beyond).not.toHaveLength(0);
    for (const hex of beyond) {
      const { longitude, latitude } = grid.centre(hex);
      expect(inside([longitude, latitude], ring)).toBe(false);
    }
  });
});
