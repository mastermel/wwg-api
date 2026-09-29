import { describe, expect, it } from "vitest";
import type { MapLayers } from "@/api/generated/model";
import { buildMapStyle, mapPalettes, nameIn, toLngLatBounds } from "@/features/maps/map-style";

const all: MapLayers = {
  roads: true,
  places: true,
  water: true,
  forests: true,
  hills: true,
  contours: true,
};

const layerIds = (layers: MapLayers, contourTiles?: string) =>
  buildMapStyle({ layers, language: "en", scheme: "light", contourTiles }).layers.map((l) => l.id);

describe("the campaign map's style", () => {
  it("draws each layer that's switched on, in order, over the land", () => {
    expect(layerIds(all, "contours://tiles")).toEqual([
      "land",
      "forests",
      "hills",
      "contours",
      "water",
      "rivers",
      "roads",
      "places",
    ]);
  });

  it("leaves out the layers that are switched off", () => {
    expect(
      layerIds({ ...all, forests: false, hills: false, roads: false, contours: false }),
    ).toEqual(["land", "water", "rivers", "places"]);
  });

  it("needs contour tiles to draw contours", () => {
    expect(layerIds(all)).not.toContain("contours");
  });

  it("only draws the main road classes: no motorways, lanes or railways", () => {
    const roads = buildMapStyle({ layers: all, language: "en", scheme: "light" }).layers.find(
      (l) => l.id === "roads",
    );

    expect(roads && "filter" in roads ? roads.filter : undefined).toEqual([
      "in",
      ["get", "class"],
      ["literal", ["trunk", "primary", "secondary"]],
    ]);
  });

  it("names places in the campaign's language, else their own name", () => {
    expect(nameIn("de")).toEqual(["coalesce", ["get", "name:de"], ["get", "name"]]);
    expect(nameIn("local")).toEqual(["get", "name"]);
  });

  it("uses the dark palette in dark mode", () => {
    const style = buildMapStyle({ layers: all, language: "en", scheme: "dark" });
    const land = style.layers.find((l) => l.id === "land");

    expect(land && "paint" in land ? land.paint : undefined).toEqual({
      "background-color": mapPalettes.dark.land,
    });
  });

  it("gives MapLibre bounds as west, south, east, north", () => {
    expect(toLngLatBounds({ west: 4.2, south: 50.5, east: 4.7, north: 50.8 })).toEqual([
      4.2, 50.5, 4.7, 50.8,
    ]);
  });
});
