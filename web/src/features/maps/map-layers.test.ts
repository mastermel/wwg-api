import { beforeEach, describe, expect, it } from "vitest";
import type { MapLayers } from "@/api/generated/model";
import { hexesAcross, loadHidden, shownRealLayers } from "@/features/maps/map-layers";

const campaign: MapLayers = {
  roads: true,
  places: true,
  water: true,
  forests: true,
  hills: true,
  contours: false,
  grid: true,
};

describe("hexesAcross", () => {
  it("counts the hexes along the view's longest side", () => {
    // About 35 km across at Waterloo, and 22 km down: 7 hexes of 5 km.
    expect(hexesAcross({ west: 4.2, south: 50.6, east: 4.7, north: 50.8 }, 5000)).toBeCloseTo(7, 0);
    // Tall and narrow: the height counts.
    expect(hexesAcross({ west: 4.4, south: 50, east: 4.41, north: 51 }, 5000)).toBeCloseTo(22, 0);
  });
});

describe("shownRealLayers", () => {
  it("hides what the viewer hid, and never shows what the campaign hides", () => {
    const shown = shownRealLayers(campaign, { real: ["forests", "contours"], game: ["grid"] });

    expect(shown).toEqual({ ...campaign, forests: false, contours: false });
  });
});

describe("loadHidden", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("is nothing when nothing's saved, or what's saved isn't ours", () => {
    expect(loadHidden("c")).toEqual({ real: [], game: [] });
    localStorage.setItem("wwg:map-layers:c", "not json");
    expect(loadHidden("c")).toEqual({ real: [], game: [] });
  });

  it("keeps only the layers it knows", () => {
    localStorage.setItem(
      "wwg:map-layers:c",
      JSON.stringify({ real: ["roads", "railways"], game: ["bridges", "units"] }),
    );

    expect(loadHidden("c")).toEqual({ real: ["roads"], game: ["bridges"] });
  });
});
