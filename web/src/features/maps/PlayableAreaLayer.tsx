import { useComputedColorScheme } from "@mantine/core";
import { useMemo } from "react";
import { Layer, Source } from "react-map-gl/maplibre";
import type { MapBounds } from "@/api/generated/model";
import { mapPalettes } from "@/features/maps/map-style";
import { playableOutline } from "@/features/maps/playable-area";

interface PlayableAreaLayerProps {
  bounds: MapBounds;
  /** The grid's hex size, or null where the campaign has no grid (then the area's rectangle). */
  hexSize: number | null;
}

// Around the whole world, for the wash outside the area.
const world: [number, number][] = [
  [-180, -85.05],
  [180, -85.05],
  [180, 85.05],
  [-180, 85.05],
  [-180, -85.05],
];

/**
 * The campaign's playable area, inside a CampaignMap: the world outside it faded (the halo
 * colour over it), and its edge outlined in the label ink, along the grid's outer hexes.
 */
export function PlayableAreaLayer({ bounds, hexSize }: PlayableAreaLayerProps) {
  const palette = mapPalettes[useComputedColorScheme("light")];
  const data = useMemo(() => {
    const rings = playableOutline(bounds, hexSize);
    return {
      type: "FeatureCollection" as const,
      features: [
        {
          type: "Feature" as const,
          properties: { kind: "outside" },
          geometry: { type: "Polygon" as const, coordinates: [world, ...rings] },
        },
        {
          type: "Feature" as const,
          properties: { kind: "edge" },
          geometry: { type: "MultiLineString" as const, coordinates: rings },
        },
      ],
    };
  }, [bounds, hexSize]);

  return (
    <Source id="playable-area" type="geojson" data={data}>
      <Layer
        id="playable-area-outside"
        type="fill"
        filter={["==", ["get", "kind"], "outside"]}
        paint={{ "fill-color": palette.halo, "fill-opacity": 0.55 }}
      />
      <Layer
        id="playable-area-edge"
        type="line"
        filter={["==", ["get", "kind"], "edge"]}
        paint={{ "line-color": palette.label, "line-opacity": 0.7, "line-width": 2 }}
      />
    </Source>
  );
}
