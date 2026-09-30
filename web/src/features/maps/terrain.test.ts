import { describe, expect, it } from "vitest";
import { hexGrid } from "@/features/maps/hex-grid";
import {
  acrossStored,
  describeSettlement,
  flowFor,
  nearestSide,
  noSettlement,
  sideCorners,
  sides,
  storedEdge,
} from "@/features/maps/terrain";

const grid = hexGrid({ west: 4.2, south: 50.6, east: 4.6, north: 50.8 }, 4828);
const hex = { q: 2, r: -1 };

describe("storedEdge", () => {
  it("keeps a hex's N, NE and SE, and gives the others to the hex across", () => {
    expect(storedEdge(hex, "NE")).toEqual({ q: 2, r: -1, side: "NE", flipped: false });
    expect(storedEdge(hex, "S")).toEqual({ q: 2, r: 0, side: "N", flipped: true });
    expect(storedEdge(hex, "SW")).toEqual({ q: 1, r: 0, side: "NE", flipped: true });
    expect(storedEdge(hex, "NW")).toEqual({ q: 1, r: -1, side: "SE", flipped: true });
  });

  it("stores each side where the hex across it is the one it came from, or its own", () => {
    for (const side of sides.slice(3)) {
      expect(acrossStored(storedEdge(hex, side))).toEqual(hex);
    }
  });
});

describe("sideCorners", () => {
  it("gives a side the same corners as the matching side of the hex across it", () => {
    const below = { q: hex.q, r: hex.r + 1 };
    const [a, b] = sideCorners(grid, hex, "S");
    const [c, d] = sideCorners(grid, below, "N");
    expect(a[0]).toBeCloseTo(d[0], 9);
    expect(a[1]).toBeCloseTo(d[1], 9);
    expect(b[0]).toBeCloseTo(c[0], 9);
    expect(b[1]).toBeCloseTo(c[1], 9);
  });
});

describe("nearestSide", () => {
  it("picks the side a point's bearing from the centre points at", () => {
    const { latitude, longitude } = grid.centre(hex);
    expect(nearestSide(grid, hex, { latitude: latitude + 0.01, longitude })).toBe("N");
    expect(nearestSide(grid, hex, { latitude: latitude - 0.01, longitude })).toBe("S");
    // East and a little north is the NE side's; a little south, the SE's.
    expect(
      nearestSide(grid, hex, { latitude: latitude + 0.005, longitude: longitude + 0.02 }),
    ).toBe("NE");
    expect(
      nearestSide(grid, hex, { latitude: latitude - 0.005, longitude: longitude + 0.02 }),
    ).toBe("SE");
  });
});

describe("flowFor", () => {
  it("turns the flow round when the edge is stored on the hex across", () => {
    expect(flowFor("Out", false)).toBe("Out");
    expect(flowFor("Out", true)).toBe("In");
    expect(flowFor("In", true)).toBe("Out");
    expect(flowFor("None", true)).toBe("None");
  });
});

describe("describeSettlement", () => {
  it("puts the parts into words", () => {
    expect(describeSettlement(noSettlement)).toBeNull();
    expect(describeSettlement({ ...noSettlement, size: "Town" })).toBe("Town");
    expect(
      describeSettlement({
        size: "City",
        walled: true,
        fortress: true,
        capital: "Capital",
        name: "Brussels",
      }),
    ).toBe("Walled capital city, with a fortress: Brussels");
    expect(describeSettlement({ ...noSettlement, fortress: true, name: "Fort Lillo" })).toBe(
      "Fortress: Fort Lillo",
    );
  });
});
