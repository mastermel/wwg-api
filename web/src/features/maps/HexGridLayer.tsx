import { useComputedColorScheme } from "@mantine/core";
import { useMemo } from "react";
import { Layer, Source } from "react-map-gl/maplibre";
import type { MapBounds } from "@/api/generated/model";
import { hexCount, hexGrid, maxDrawnHexes } from "@/features/maps/hex-grid";
import { mapPalettes } from "@/features/maps/map-style";

interface HexGridLayerProps {
  bounds: MapBounds;
  /** Hexes' size across the flats, in metres. */
  size: number;
  /** Whether it's shown; hidden rather than removed, so showing it again is immediate. */
  visible?: boolean;
}

// MapLibre's 512-pixel tiles: metres per pixel at zoom 0 on the equator.
const metresPerPixelAtZoom0 = 78_271.517;
// Below this many pixels across, the grid is a blur: it's hidden until zoomed in that far.
const minPixels = 12;

/**
 * The campaign's hex grid (decision 0014), drawn as thin lines over the map, inside a
 * CampaignMap. It appears from the zoom where a hex is about 12 px across. Nothing is drawn for
 * a grid of more than `maxDrawnHexes`: check `hexCount` first to say so.
 */
export function HexGridLayer({ bounds, size, visible = true }: HexGridLayerProps) {
  const ink = mapPalettes[useComputedColorScheme("light")].label;
  const outlines = useMemo(() => {
    if (hexCount(bounds, size) > maxDrawnHexes)
      return { type: "FeatureCollection" as const, features: [] };
    const grid = hexGrid(bounds, size);
    return {
      type: "FeatureCollection" as const,
      features: grid.hexes().map((hex) => {
        const corners = grid.corners(hex);
        return {
          type: "Feature" as const,
          properties: {},
          geometry: { type: "LineString" as const, coordinates: [...corners, corners[0]] },
        };
      }),
    };
  }, [bounds, size]);
  const latitude = (bounds.south + bounds.north) / 2;
  const minzoom = Math.max(
    0,
    Math.log2((metresPerPixelAtZoom0 * Math.cos((latitude * Math.PI) / 180) * minPixels) / size),
  );

  return (
    <Source id="hex-grid" type="geojson" data={outlines}>
      <Layer
        id="hex-grid"
        type="line"
        minzoom={minzoom}
        layout={{ visibility: visible ? "visible" : "none" }}
        paint={{ "line-color": ink, "line-opacity": 0.35, "line-width": 1 }}
      />
    </Source>
  );
}
