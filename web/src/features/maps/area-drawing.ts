import type { MapBounds } from "@/api/generated/model";
import type { MapPointer } from "@/features/maps/CampaignMap";

/**
 * Drawing the campaign's area on the map (Map settings): drag a rectangle with the mouse, or
 * press one corner and then the opposite one (a phone's taps, or two clicks). While a corner's
 * down, the rectangle follows the pointer.
 */
export type AreaDrawing =
  | { phase: "idle" }
  /** Drawing, no corner yet. */
  | { phase: "waiting" }
  /** One corner set; `to` follows the pointer. `pressed` from where it went down, while it's down. */
  | {
      phase: "anchored";
      from: Corner;
      to: Corner;
      pressed: { x: number; y: number } | null;
      /** The first corner's press is over: the next press and lift sets the other corner. */
      placed: boolean;
    };

interface Corner {
  longitude: number;
  latitude: number;
}

export type DrawingStep =
  { drawing: AreaDrawing; done?: undefined } | { drawing: AreaDrawing; done: MapBounds };

/** Further than this, in pixels, between pressing and lifting, is a drag, not a click. */
export const dragPixels = 6;

/** The rectangle two corners make, west to east and south to north. */
export const rectangle = (a: Corner, b: Corner): MapBounds => ({
  west: Math.min(a.longitude, b.longitude),
  south: Math.min(a.latitude, b.latitude),
  east: Math.max(a.longitude, b.longitude),
  north: Math.max(a.latitude, b.latitude),
});

/** A rectangle with no width or height isn't an area. */
const empty = (bounds: MapBounds) => bounds.west === bounds.east || bounds.south === bounds.north;

/** The next state after a pointer event; `done` when a rectangle's finished. */
export function drawStep(drawing: AreaDrawing, pointer: MapPointer): DrawingStep {
  const at = { longitude: pointer.longitude, latitude: pointer.latitude };
  if (drawing.phase === "idle") return { drawing };

  if (drawing.phase === "waiting") {
    return pointer.kind === "down"
      ? {
          drawing: {
            phase: "anchored",
            from: at,
            to: at,
            pressed: { x: pointer.x, y: pointer.y },
            placed: false,
          },
        }
      : { drawing };
  }

  switch (pointer.kind) {
    case "move":
      return { drawing: { ...drawing, to: at } };
    case "down":
      return { drawing: { ...drawing, to: at, pressed: { x: pointer.x, y: pointer.y } } };
    case "up": {
      const moved =
        drawing.pressed !== null &&
        Math.hypot(pointer.x - drawing.pressed.x, pointer.y - drawing.pressed.y) > dragPixels;
      // A click on the first corner: wait for the other one.
      if (!drawing.placed && !moved) {
        return { drawing: { ...drawing, to: at, pressed: null, placed: true } };
      }
      const bounds = rectangle(drawing.from, at);
      // Nothing drawn (the same point twice): start again.
      if (empty(bounds)) return { drawing: { phase: "waiting" } };
      return { drawing: { phase: "idle" }, done: bounds };
    }
  }
}

/** The rectangle to show while drawing, once there's a corner. */
export const preview = (drawing: AreaDrawing): MapBounds | null =>
  drawing.phase === "anchored" ? rectangle(drawing.from, drawing.to) : null;
