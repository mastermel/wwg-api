import type { MapBounds } from "@/api/generated/model";
import { hexCount, hexGrid, maxDrawnHexes } from "@/features/maps/hex-grid";

/**
 * The campaign's playable area on the map: how far the view may go past it (enough that the
 * whole area fits a map of any shape), and its outline (the grid's outer edge, hex by hex), the
 * rest of the world dimmed around it.
 */

const earthRadius = 6_371_008.8;
const radians = (degrees: number) => (degrees * Math.PI) / 180;
const degrees = (value: number) => (value * 180) / Math.PI;
// Web Mercator, as MapLibre draws: x is the longitude in radians, y grows northwards.
const mercatorY = (latitude: number) => Math.log(Math.tan(Math.PI / 4 + radians(latitude) / 2));
const latitudeAt = (y: number) => degrees(2 * Math.atan(Math.exp(y)) - Math.PI / 2);
// Where Web Mercator stops.
const maxLatitude = 85.05;

/** How far past the area the view may go, as a share of its longest side. */
export const viewMargin = 0.1;

/**
 * The most the view may take in (MapLibre's maxBounds): the area, a margin around it (a tenth
 * of its longest side, and at least a hex, so the grid's edge hexes show whole), widened one way
 * to the map's shape (`aspect`, its width over its height), so zooming out shows all of it.
 */
export function viewLimits(area: MapBounds, aspect: number, hexSize = 0): MapBounds {
  const x0 = radians(area.west);
  const x1 = radians(area.east);
  const y0 = mercatorY(area.south);
  const y1 = mercatorY(area.north);
  const latitude = radians((area.south + area.north) / 2);
  const hex = hexSize / (earthRadius * Math.cos(latitude));
  const pad = Math.max(viewMargin * Math.max(x1 - x0, y1 - y0), hex);
  let width = x1 - x0 + 2 * pad;
  let height = y1 - y0 + 2 * pad;
  if (aspect > 0 && Number.isFinite(aspect)) {
    if (width / height < aspect) width = height * aspect;
    else height = width / aspect;
  }
  const x = (x0 + x1) / 2;
  const y = (y0 + y1) / 2;
  return {
    west: Math.max(-180, degrees(x - width / 2)),
    east: Math.min(180, degrees(x + width / 2)),
    south: Math.max(-maxLatitude, latitudeAt(y - height / 2)),
    north: Math.min(maxLatitude, latitudeAt(y + height / 2)),
  };
}

type Position = [number, number];

/** A corner's key: hexes sharing it work it out a hair apart. */
const keyOf = ([longitude, latitude]: Position) => `${longitude.toFixed(7)},${latitude.toFixed(7)}`;

/**
 * The playable area's outline, as closed rings of [longitude, latitude]: the grid's outer edge
 * (the sides of its hexes with no hex of the grid beyond), or the area's rectangle where there's
 * no grid, or one too big to draw.
 */
export function playableOutline(area: MapBounds, hexSize: number | null): Position[][] {
  if (hexSize === null || hexCount(area, hexSize) > maxDrawnHexes) {
    return [
      [
        [area.west, area.north],
        [area.east, area.north],
        [area.east, area.south],
        [area.west, area.south],
        [area.west, area.north],
      ],
    ];
  }

  const grid = hexGrid(area, hexSize);
  // Each hex's corners run clockwise, so its outer sides chain into rings, end to start.
  const next = new Map<string, Position>();
  for (const hex of grid.hexes()) {
    const centre = grid.centre(hex);
    const corners = grid.corners(hex);
    corners.forEach((from, i) => {
      const to = corners[(i + 1) % 6] ?? from;
      // The hex beyond this side: its centre is the side's middle, as far again.
      const beyond = grid.hexAt({
        longitude: from[0] + to[0] - centre.longitude,
        latitude: from[1] + to[1] - centre.latitude,
      });
      if (!grid.contains(beyond)) next.set(keyOf(from), to);
    });
  }

  const rings: Position[][] = [];
  const seen = new Set<string>();
  for (const [key, first] of next) {
    if (seen.has(key)) continue;
    const ring: Position[] = [];
    let at: string = key;
    let to: Position | undefined = first;
    while (to && !seen.has(at)) {
      seen.add(at);
      ring.push(to);
      at = keyOf(to);
      to = next.get(at);
    }
    // Closed: it ends where it began.
    if (ring.length > 2) rings.push([ring.at(-1) ?? ring[0], ...ring]);
  }
  return rings;
}

/**
 * The middle of the area cropped to the map's shape (`aspect`, its width over its height): framed
 * on it, the area fills the map, as a phone opens it.
 */
export function fillingView(area: MapBounds, aspect: number): MapBounds {
  const x0 = radians(area.west);
  const x1 = radians(area.east);
  const y0 = mercatorY(area.south);
  const y1 = mercatorY(area.north);
  let width = x1 - x0;
  let height = y1 - y0;
  if (width / height > aspect) width = height * aspect;
  else height = width / aspect;
  const x = (x0 + x1) / 2;
  const y = (y0 + y1) / 2;
  return {
    west: degrees(x - width / 2),
    east: degrees(x + width / 2),
    south: latitudeAt(y - height / 2),
    north: latitudeAt(y + height / 2),
  };
}
