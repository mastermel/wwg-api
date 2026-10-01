import { useComputedColorScheme } from "@mantine/core";
import type { FeatureCollection, Polygon } from "geojson";
import { Layer, Source } from "react-map-gl/maplibre";
import type { Hex, HexGrid } from "@/features/maps/hex-grid";
import { mapPalettes } from "@/features/maps/map-style";

/**
 * The hex whose card is open, outlined in the label colour (9.5:1 or more against the map).
 * Mounted from the start and hidden when there's none: WebKit can miss layers added later.
 */
export function SelectedHexLayer({ grid, hex }: { grid: HexGrid; hex: Hex | null }) {
  const { label } = mapPalettes[useComputedColorScheme("light")];
  const corners = hex ? grid.corners(hex) : [];
  const data: FeatureCollection<Polygon> = {
    type: "FeatureCollection",
    features: hex
      ? [
          {
            type: "Feature",
            properties: {},
            geometry: { type: "Polygon", coordinates: [[...corners, corners[0] ?? [0, 0]]] },
          },
        ]
      : [],
  };
  return (
    <Source id="selected-hex" type="geojson" data={data}>
      <Layer
        id="selected-hex"
        type="line"
        layout={{ visibility: hex ? "visible" : "none" }}
        paint={{ "line-color": label, "line-width": 2.5 }}
      />
    </Source>
  );
}
