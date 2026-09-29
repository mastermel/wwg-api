import { describe, expect, it } from "vitest";
import { circle, distanceMetres, inBounds } from "@/features/maps/geo";

const waterloo = { latitude: 50.7, longitude: 4.4 };

describe("distanceMetres", () => {
  it("measures a degree of latitude as about 111 km", () => {
    expect(distanceMetres(waterloo, { latitude: 51.7, longitude: 4.4 })).toBeCloseTo(111_195, -1);
  });

  it("is nothing from a point to itself", () => {
    expect(distanceMetres(waterloo, waterloo)).toBe(0);
  });
});

describe("inBounds", () => {
  const area = { west: 4.2, south: 50.6, east: 4.6, north: 50.8 };

  it("includes points inside and on the edge", () => {
    expect(inBounds(waterloo, area)).toBe(true);
    expect(inBounds({ latitude: 50.8, longitude: 4.2 }, area)).toBe(true);
  });

  it("excludes points outside", () => {
    expect(inBounds({ latitude: 50.85, longitude: 4.4 }, area)).toBe(false);
  });
});

describe("circle", () => {
  it("is a closed ring of points all the distance away", () => {
    const ring = circle(waterloo, 5000, 16);

    expect(ring).toHaveLength(17);
    expect(ring[0]).toEqual(ring[16]);
    for (const [longitude, latitude] of ring) {
      expect(distanceMetres(waterloo, { latitude, longitude })).toBeCloseTo(5000, 0);
    }
  });
});
