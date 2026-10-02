import { describe, expect, it } from "vitest";
import expected from "../../../../testdata/hex-grid.json";
import { distanceMetres } from "@/features/maps/geo";
import {
  gridDimensions,
  hexCount,
  hexDimensions,
  hexDistance,
  hexGrid,
  neighbours,
} from "@/features/maps/hex-grid";

describe.each(expected.cases)(
  "the hex grid: $name",
  ({ bounds, hexSize, points, hexCount: count, outside }) => {
    const grid = hexGrid(bounds, hexSize);

    it("puts each point in the expected hex, with the expected centre", () => {
      for (const point of points) {
        const hex = grid.hexAt(point);
        expect(hex).toEqual({ q: point.q, r: point.r });
        const centre = grid.centre(hex);
        expect(centre.latitude).toBeCloseTo(point.centre.latitude, 6);
        expect(centre.longitude).toBeCloseTo(point.centre.longitude, 6);
      }
    });

    it("covers the area with the expected number of hexes", () => {
      expect(grid.hexes()).toHaveLength(count);
      // The estimate, without laying the grid out, is close.
      expect(Math.abs(hexCount(bounds, hexSize) - count) / count).toBeLessThan(0.1);
      for (const hex of outside) expect(grid.contains(hex)).toBe(false);
    });

    it("makes hexes the size asked for, across the flats", () => {
      const centre = grid.centre({ q: 0, r: 0 });
      for (const next of neighbours({ q: 0, r: 0 })) {
        expect(distanceMetres(centre, grid.centre(next)) / hexSize).toBeCloseTo(1, 2);
      }
      expect(grid.corners({ q: 0, r: 0 })).toHaveLength(6);
    });
  },
);

describe("hexDistance", () => {
  it("counts the steps between hexes", () => {
    expect(hexDistance({ q: 0, r: 0 }, { q: 0, r: 0 })).toBe(0);
    expect(hexDistance({ q: 0, r: 0 }, { q: 1, r: -1 })).toBe(1);
    expect(hexDistance({ q: -3, r: 4 }, { q: 3, r: -4 })).toBe(8);
  });
});

describe("the area's size in hexes", () => {
  // Waterloo and around, at 3 miles and at 2 km: as laid out, and as estimated.
  const waterloo = { west: 4.2, south: 50.55, east: 4.7, north: 50.8 };

  it("counts a laid-out grid's columns and the most hexes down one", () => {
    expect(gridDimensions(hexGrid(waterloo, 4828).hexes())).toEqual({ across: 9, down: 6 });
    expect(gridDimensions(hexGrid(waterloo, 2000).hexes())).toEqual({ across: 21, down: 14 });
  });

  it("estimates the same without laying it out", () => {
    expect(hexDimensions(waterloo, 4828)).toEqual({ across: 9, down: 6 });
    expect(hexDimensions(waterloo, 2000)).toEqual({ across: 21, down: 14 });
  });
});
