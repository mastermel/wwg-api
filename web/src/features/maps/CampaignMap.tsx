import "maplibre-gl/dist/maplibre-gl.css";
import "@/features/maps/maplibre-worker";

import { useComputedColorScheme } from "@mantine/core";
import { useMemo, type ReactNode } from "react";
import MapGL, { AttributionControl, NavigationControl, type MapRef } from "react-map-gl/maplibre";
import type { CampaignMapResponse, MapBounds } from "@/api/generated/model";
import classes from "@/features/maps/CampaignMap.module.css";
import { contourTiles } from "@/features/maps/contours";
import { attribution, buildMapStyle, toLngLatBounds } from "@/features/maps/map-style";

interface CampaignMapProps {
  settings: CampaignMapResponse;
  /** Where the map opens; also the area it's held inside, unless `free`. */
  bounds: MapBounds;
  /** Let the map go anywhere (the Umpire setting the area). */
  free?: boolean;
  /** Called with the map, once it's loaded (e.g. to read the view the Umpire chose). */
  mapRef?: React.Ref<MapRef>;
  children?: ReactNode;
}

/**
 * A campaign's map (DESIGN.md §3.13): our style from its settings, in the current colour
 * scheme, held inside its bounds (MapLibre's maxBounds: nothing outside them can be seen, and
 * zooming out stops there); zooming in is unlimited.
 */
export function CampaignMap({
  settings,
  bounds,
  free = false,
  mapRef,
  children,
}: CampaignMapProps) {
  const scheme = useComputedColorScheme("light");
  const { layers, labelLanguage, distanceUnit } = settings;
  const mapStyle = useMemo(
    () =>
      buildMapStyle({
        layers,
        language: labelLanguage,
        scheme,
        contourTiles: layers.contours ? contourTiles(distanceUnit === "Miles") : undefined,
      }),
    [layers, labelLanguage, scheme, distanceUnit],
  );

  return (
    <div className={classes.map}>
      <MapGL
        ref={mapRef}
        mapStyle={mapStyle}
        initialViewState={{ bounds: toLngLatBounds(bounds) }}
        maxBounds={free ? undefined : toLngLatBounds(bounds)}
        attributionControl={false}
        dragRotate={false}
        touchPitch={false}
        pitchWithRotate={false}
        style={{ width: "100%", height: "100%" }}
      >
        <NavigationControl position="top-right" showCompass={false} />
        <AttributionControl position="bottom-right" customAttribution={attribution} compact />
        {children}
      </MapGL>
    </div>
  );
}
