import "maplibre-gl/dist/maplibre-gl.css";
import "@/features/maps/maplibre-worker";

import { useComputedColorScheme } from "@mantine/core";
import { useMediaQuery } from "@mantine/hooks";
import { useMemo, type ReactNode } from "react";
import type { LngLatBounds } from "maplibre-gl";
import MapGL, { AttributionControl, NavigationControl, type MapRef } from "react-map-gl/maplibre";
import type { CampaignMapResponse, MapBounds } from "@/api/generated/model";
import classes from "@/features/maps/CampaignMap.module.css";
import { contourTiles } from "@/features/maps/contours";
import { HexGridLayer } from "@/features/maps/HexGridLayer";
import { attribution, buildMapStyle, toLngLatBounds } from "@/features/maps/map-style";

export interface MapPointer {
  kind: "down" | "move" | "up";
  longitude: number;
  latitude: number;
  x: number;
  y: number;
}

interface CampaignMapProps {
  settings: CampaignMapResponse;
  /** Where the map opens; also the area it's held inside, unless `free`. */
  bounds: MapBounds;
  /** Let the map go anywhere (the Umpire setting the area). */
  free?: boolean;
  /** Called with the map, once it's loaded (e.g. to read the view the Umpire chose). */
  mapRef?: React.Ref<MapRef>;
  /** A click (or tap) on the map itself, not on a marker, at a point in degrees. */
  onMapClick?: (point: { longitude: number; latitude: number }) => void;
  /** The cursor over the map, e.g. "crosshair" while choosing a point. */
  cursor?: string;
  /** Whether dragging pans the map (not while the Umpire draws the area). */
  dragPan?: boolean;
  /**
   * A pointer (mouse or finger) pressed, moved or lifted over the map, at a point in degrees and
   * in pixels on the map: for drawing on it.
   */
  onPointer?: (event: MapPointer) => void;
  /** The area in view, once the map loads and as it moves (the Map page's game map, by zoom). */
  onViewChange?: (view: MapBounds) => void;
  /** Whether to show the hex grid, where the settings draw it (shown unless given). */
  grid?: boolean;
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
  onMapClick,
  cursor,
  dragPan = true,
  onPointer,
  onViewChange,
  grid,
  children,
}: CampaignMapProps) {
  const scheme = useComputedColorScheme("light");
  // On a touch screen one finger scrolls the page past the map, and two move it (MapLibre shows
  // a hint); taps still reach it. Not with a mouse, where the wheel would then need Ctrl to zoom.
  // Read at once, not after mounting: MapLibre only takes the setting when the map is made.
  const touch = useMediaQuery("(pointer: coarse)", undefined, { getInitialValueInEffect: false });
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
        cooperativeGestures={touch}
        dragRotate={false}
        touchPitch={false}
        pitchWithRotate={false}
        cursor={cursor}
        dragPan={dragPan}
        {...(onPointer ? pointerHandlers(onPointer) : {})}
        onClick={(event) =>
          onMapClick?.({ longitude: event.lngLat.lng, latitude: event.lngLat.lat })
        }
        onLoad={(event) => onViewChange?.(viewOf(event.target))}
        onMove={(event) => onViewChange?.(viewOf(event.target))}
        style={{ width: "100%", height: "100%" }}
      >
        <NavigationControl position="top-right" showCompass={false} />
        <AttributionControl position="bottom-right" customAttribution={attribution} compact />
        {/* Under the units. The settings page draws its own, from the size being chosen. */}
        {!free && layers.grid && (
          <HexGridLayer bounds={bounds} size={settings.hexSize} visible={grid ?? true} />
        )}
        {children}
      </MapGL>
    </div>
  );
}

/** The area a map shows. */
function viewOf(map: { getBounds: () => LngLatBounds }): MapBounds {
  const view = map.getBounds();
  return {
    west: view.getWest(),
    south: view.getSouth(),
    east: view.getEast(),
    north: view.getNorth(),
  };
}

/** MapLibre's mouse and touch events, as one kind of pointer. */
function pointerHandlers(onPointer: (event: MapPointer) => void) {
  const send =
    (kind: MapPointer["kind"]) =>
    (event: { lngLat: { lng: number; lat: number }; point: { x: number; y: number } }) => {
      onPointer({
        kind,
        longitude: event.lngLat.lng,
        latitude: event.lngLat.lat,
        x: event.point.x,
        y: event.point.y,
      });
    };
  return {
    onMouseDown: send("down"),
    onMouseMove: send("move"),
    onMouseUp: send("up"),
    onTouchStart: send("down"),
    onTouchMove: send("move"),
    onTouchEnd: send("up"),
  };
}
