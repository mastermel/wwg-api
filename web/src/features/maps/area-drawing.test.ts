import { describe, expect, it } from "vitest";
import type { MapPointer } from "@/features/maps/CampaignMap";
import { drawStep, preview, rectangle, type AreaDrawing } from "@/features/maps/area-drawing";

const at = (kind: MapPointer["kind"], longitude: number, latitude: number, x: number, y: number) =>
  ({ kind, longitude, latitude, x, y }) satisfies MapPointer;

/** Runs the pointer events from a fresh drawing, returning where it got. */
function run(...events: MapPointer[]) {
  let drawing: AreaDrawing = { phase: "waiting" };
  let done;
  for (const event of events) {
    const step = drawStep(drawing, event);
    drawing = step.drawing;
    done = step.done ?? done;
  }
  return { drawing, done };
}

describe("drawing the area", () => {
  it("takes a drag as the rectangle, whichever way it's drawn", () => {
    const { drawing, done } = run(
      at("down", 4.6, 50.6, 300, 200),
      at("move", 4.4, 50.7, 200, 150),
      at("up", 4.2, 50.8, 100, 100),
    );

    expect(done).toEqual({ west: 4.2, south: 50.6, east: 4.6, north: 50.8 });
    expect(drawing).toEqual({ phase: "idle" });
  });

  it("takes two clicks as opposite corners, following the pointer between them", () => {
    const first = run(at("down", 4.2, 50.8, 100, 100), at("up", 4.2, 50.8, 101, 100));
    expect(first.done).toBeUndefined();

    const following = drawStep(first.drawing, at("move", 4.5, 50.65, 250, 180));
    expect(preview(following.drawing)).toEqual({ west: 4.2, south: 50.65, east: 4.5, north: 50.8 });

    const pressed = drawStep(following.drawing, at("down", 4.6, 50.6, 300, 200));
    expect(drawStep(pressed.drawing, at("up", 4.6, 50.6, 300, 200)).done).toEqual({
      west: 4.2,
      south: 50.6,
      east: 4.6,
      north: 50.8,
    });
  });

  it("starts again if both corners are the same point", () => {
    const { drawing, done } = run(
      at("down", 4.2, 50.8, 100, 100),
      at("up", 4.2, 50.8, 100, 100),
      at("down", 4.2, 50.8, 100, 100),
      at("up", 4.2, 50.8, 100, 100),
    );

    expect(done).toBeUndefined();
    expect(drawing).toEqual({ phase: "waiting" });
  });

  it("does nothing until drawing starts", () => {
    expect(drawStep({ phase: "idle" }, at("down", 4.2, 50.8, 1, 1))).toEqual({
      drawing: { phase: "idle" },
    });
    expect(preview({ phase: "waiting" })).toBeNull();
    expect(rectangle({ longitude: 1, latitude: 2 }, { longitude: 0, latitude: 3 })).toEqual({
      west: 0,
      south: 2,
      east: 1,
      north: 3,
    });
  });
});
