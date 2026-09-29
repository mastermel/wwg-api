import type { ExpressionSpecification, LayerSpecification, StyleSpecification } from "maplibre-gl";
import type { MapBounds, MapLayers } from "@/api/generated/model";

/**
 * The campaign map's style (DESIGN.md §3.13, decision 0009): only what a Napoleonic campaign
 * needs, from OpenFreeMap's OpenMapTiles vector tiles, with Mapterhorn hillshading. Motorways,
 * railways, borders, buildings and points of interest aren't drawn at all.
 */

/** Tile hosts, also allowed by the CSP (the API's SpaHostingExtensions). */
export const tileHosts = {
  vector: "https://tiles.openfreemap.org/planet",
  glyphs: "https://tiles.openfreemap.org/fonts/{fontstack}/{range}.pbf",
  elevation: "https://tiles.mapterhorn.com/{z}/{x}/{y}.webp",
} as const;

/**
 * Credit beyond what the tiles carry: OpenFreeMap's TileJSON already credits OpenFreeMap,
 * OpenMapTiles and OpenStreetMap (MapLibre shows it); the elevation tiles don't.
 */
export const attribution =
  '<a href="https://mapterhorn.com" target="_blank" rel="noreferrer">Mapterhorn</a>';

/**
 * Per colour scheme. Checked with the WCAG formula: labels are 9.5:1 or more against land, forest
 * and water (and have a halo); roads and rivers are at least 3:1 against land, forest and water
 * in both schemes (WCAG 1.4.11). Unit icons (step 32) carry their own outline.
 */
export const mapPalettes = {
  light: {
    land: "#eef0e6",
    forest: "#d3e2c4",
    water: "#b8d4ea",
    river: "#3f76a8",
    road: "#a0662c",
    label: "#1f2a30",
    halo: "#ffffff",
    contour: "rgba(60, 50, 30, 0.35)",
    hillShadow: "#3d4a3a",
    hillHighlight: "#ffffff",
  },
  dark: {
    land: "#1d2126",
    forest: "#233329",
    water: "#1b3347",
    river: "#5b93c7",
    road: "#d49a55",
    label: "#e9edf1",
    halo: "#11151a",
    contour: "rgba(220, 225, 235, 0.3)",
    hillShadow: "#000000",
    hillHighlight: "#5a6675",
  },
} as const;

export type MapScheme = keyof typeof mapPalettes;

/** A place's name in `language` ("local": its own), falling back to its own where there's none. */
export function nameIn(language: string): ExpressionSpecification {
  return language === "local"
    ? ["get", "name"]
    : ["coalesce", ["get", `name:${language}`], ["get", "name"]];
}

interface MapStyleOptions {
  layers: MapLayers;
  language: string;
  scheme: MapScheme;
  /** The contour lines' tile URL (maplibre-contour), when they're shown. */
  contourTiles?: string;
}

export function buildMapStyle({
  layers,
  language,
  scheme,
  contourTiles,
}: MapStyleOptions): StyleSpecification {
  const p = mapPalettes[scheme];
  const drawn: LayerSpecification[] = [
    { id: "land", type: "background", paint: { "background-color": p.land } },
  ];

  if (layers.forests) {
    drawn.push({
      id: "forests",
      type: "fill",
      source: "openmaptiles",
      "source-layer": "landcover",
      filter: ["==", ["get", "class"], "wood"],
      paint: { "fill-color": p.forest },
    });
  }

  if (layers.hills) {
    drawn.push({
      id: "hills",
      type: "hillshade",
      source: "elevation",
      paint: {
        "hillshade-exaggeration": 0.35,
        "hillshade-shadow-color": p.hillShadow,
        "hillshade-highlight-color": p.hillHighlight,
        "hillshade-accent-color": p.hillShadow,
      },
    });
  }

  if (layers.contours && contourTiles) {
    drawn.push({
      id: "contours",
      type: "line",
      source: "contours",
      "source-layer": "contours",
      paint: {
        "line-color": p.contour,
        "line-width": ["match", ["get", "level"], 1, 1, 0.5],
      },
    });
  }

  if (layers.water) {
    drawn.push(
      {
        id: "water",
        type: "fill",
        source: "openmaptiles",
        "source-layer": "water",
        paint: { "fill-color": p.water },
      },
      {
        id: "rivers",
        type: "line",
        source: "openmaptiles",
        "source-layer": "waterway",
        filter: ["in", ["get", "class"], ["literal", ["river", "canal", "stream"]]],
        paint: {
          "line-color": p.river,
          "line-width": [
            "interpolate",
            ["linear"],
            ["zoom"],
            6,
            ["match", ["get", "class"], "river", 1, 0],
            12,
            ["match", ["get", "class"], "river", 3, "canal", 2, 0.8],
          ],
        },
      },
    );
  }

  if (layers.roads) {
    drawn.push({
      id: "roads",
      type: "line",
      source: "openmaptiles",
      "source-layer": "transportation",
      // The main roads, much as the period's high roads ran; not motorways (new routes) or the
      // lanes, tracks and paths of today.
      filter: ["in", ["get", "class"], ["literal", ["trunk", "primary", "secondary"]]],
      layout: { "line-cap": "round", "line-join": "round" },
      paint: {
        "line-color": p.road,
        "line-width": [
          "interpolate",
          ["linear"],
          ["zoom"],
          6,
          ["match", ["get", "class"], "secondary", 0.4, 0.8],
          12,
          ["match", ["get", "class"], "secondary", 1.5, 2.5],
        ],
      },
    });
  }

  if (layers.places) {
    drawn.push({
      id: "places",
      type: "symbol",
      source: "openmaptiles",
      "source-layer": "place",
      filter: ["in", ["get", "class"], ["literal", ["city", "town", "village"]]],
      layout: {
        "text-field": nameIn(language),
        "text-font": [
          "match",
          ["get", "class"],
          "city",
          ["literal", ["Noto Sans Bold"]],
          ["literal", ["Noto Sans Regular"]],
        ],
        "text-size": ["match", ["get", "class"], "city", 15, "town", 13, 11],
        "symbol-sort-key": ["match", ["get", "class"], "city", 0, "town", 1, 2],
      },
      paint: { "text-color": p.label, "text-halo-color": p.halo, "text-halo-width": 1.5 },
    });
  }

  return {
    version: 8,
    glyphs: tileHosts.glyphs,
    sources: {
      openmaptiles: { type: "vector", url: tileHosts.vector },
      elevation: {
        type: "raster-dem",
        tiles: [tileHosts.elevation],
        encoding: "terrarium",
        tileSize: 512,
        maxzoom: 12,
      },
      ...(layers.contours && contourTiles
        ? { contours: { type: "vector" as const, tiles: [contourTiles], maxzoom: 15 } }
        : {}),
    },
    layers: drawn,
  };
}

/** [west, south, east, north], as MapLibre takes bounds. */
export const toLngLatBounds = (b: MapBounds): [number, number, number, number] => [
  b.west,
  b.south,
  b.east,
  b.north,
];
