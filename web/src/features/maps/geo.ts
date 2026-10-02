import type { MapBounds } from "@/api/generated/model";

/** A point in degrees. */
export interface Point {
  latitude: number;
  longitude: number;
}

// The mean Earth radius, as the API's hex grid uses (HexGrid.cs).
const earthRadius = 6_371_008.8;
const toRadians = (degrees: number) => (degrees * Math.PI) / 180;
const toDegrees = (radians: number) => (radians * 180) / Math.PI;

/** The straight-line (great-circle) distance between two points, in metres. */
export function distanceMetres(from: Point, to: Point) {
  const dLat = toRadians(to.latitude - from.latitude);
  const dLon = toRadians(to.longitude - from.longitude);
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRadians(from.latitude)) * Math.cos(toRadians(to.latitude)) * Math.sin(dLon / 2) ** 2;
  return 2 * earthRadius * Math.asin(Math.min(1, Math.sqrt(a)));
}

/** Whether a point is inside the campaign's area. */
export const inBounds = ({ latitude, longitude }: Point, bounds: MapBounds) =>
  latitude >= bounds.south &&
  latitude <= bounds.north &&
  longitude >= bounds.west &&
  longitude <= bounds.east;

/**
 * The ring of points `metres` from `centre`, as [longitude, latitude] pairs (GeoJSON's order),
 * closed: a unit's range drawn on the map.
 */
export function circle(centre: Point, metres: number, steps = 64): [number, number][] {
  const lat = toRadians(centre.latitude);
  const lon = toRadians(centre.longitude);
  const angular = metres / earthRadius;
  const ring: [number, number][] = [];
  for (let step = 0; step <= steps; step++) {
    const bearing = (2 * Math.PI * step) / steps;
    const pointLat = Math.asin(
      Math.sin(lat) * Math.cos(angular) + Math.cos(lat) * Math.sin(angular) * Math.cos(bearing),
    );
    const pointLon =
      lon +
      Math.atan2(
        Math.sin(bearing) * Math.sin(angular) * Math.cos(lat),
        Math.cos(angular) - Math.sin(lat) * Math.sin(pointLat),
      );
    ring.push([toDegrees(pointLon), toDegrees(pointLat)]);
  }
  return ring;
}
