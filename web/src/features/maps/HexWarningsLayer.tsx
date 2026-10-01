import { useComputedColorScheme } from "@mantine/core";
import type { FeatureCollection, Polygon } from "geojson";
import { Layer, Source } from "react-map-gl/maplibre";
import type { HexWarning } from "@/features/maps/contact";
import type { HexGrid } from "@/features/maps/hex-grid";
import { mapPalettes } from "@/features/maps/map-style";

/**
 * The hexes to warn of (contact and concentration, step 46), outlined. Inside a CampaignMap.
 * Drawn for sight only: the turn panel lists the same hexes in words.
 */
export function HexWarningsLayer({
  grid,
  warnings,
}: {
  grid: HexGrid;
  warnings: readonly HexWarning[];
}) {
  const { warning } = mapPalettes[useComputedColorScheme("light")];
  const hexes: FeatureCollection<Polygon> = {
    type: "FeatureCollection",
    features: warnings.map(({ hex }) => {
      const corners = grid.corners(hex);
      return {
        type: "Feature",
        properties: {},
        geometry: { type: "Polygon", coordinates: [[...corners, corners[0] ?? [0, 0]]] },
      };
    }),
  };
  return (
    <Source id="hex-warnings" type="geojson" data={hexes}>
      <Layer
        id="hex-warnings-fill"
        type="fill"
        paint={{ "fill-color": warning, "fill-opacity": 0.15 }}
      />
      <Layer
        id="hex-warnings-edge"
        type="line"
        paint={{ "line-color": warning, "line-width": 3 }}
      />
    </Source>
  );
}
