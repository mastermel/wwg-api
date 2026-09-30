import { useComputedColorScheme } from "@mantine/core";
import type { FeatureCollection, LineString, Polygon } from "geojson";
import { Layer, Marker, Source } from "react-map-gl/maplibre";
import { armyColorVar } from "@/features/armies/identity/army-colors";
import type { Point } from "@/features/maps/geo";
import { mapPalettes } from "@/features/maps/map-style";
import classes from "@/features/maps/OrderOverlay.module.css";
import type { PlacedUnit } from "@/features/maps/stacks";
import { UnitSymbol } from "@/features/units/UnitSymbol";

/** A unit's move this turn, not yet approved: from where it is along `line` to `to`. */
export interface PendingMove {
  placed: PlacedUnit;
  to: Point;
  /** The hex centres it passes through, as [longitude, latitude], from where it is to `to`. */
  line: [number, number][];
}

interface OrderOverlayProps {
  moves: readonly PendingMove[];
  /** The hexes the unit being moved can reach, each a ring of corners as [longitude, latitude]. */
  reachable?: readonly [number, number][][];
}

/**
 * Moves not yet approved (DESIGN.md §3.13): a ghost of each unit where it's going, with a dashed
 * line along its path; and while moving one, the hexes it can reach, shaded. Inside a CampaignMap. Drawn for sight
 * only: the turn panel lists the same orders for screen readers and keyboards.
 */
export function OrderOverlay({ moves, reachable = [] }: OrderOverlayProps) {
  const ink = mapPalettes[useComputedColorScheme("light")].label;
  const lines: FeatureCollection<LineString> = {
    type: "FeatureCollection",
    features: moves.map(({ line }) => ({
      type: "Feature",
      properties: {},
      geometry: { type: "LineString", coordinates: line },
    })),
  };
  const area: FeatureCollection<Polygon> = {
    type: "FeatureCollection",
    features: reachable.map((corners) => ({
      type: "Feature",
      properties: {},
      geometry: { type: "Polygon", coordinates: [[...corners, corners[0] ?? [0, 0]]] },
    })),
  };

  return (
    <>
      <Source id="unit-range" type="geojson" data={area}>
        <Layer
          id="unit-range-fill"
          type="fill"
          paint={{ "fill-color": ink, "fill-opacity": 0.12 }}
        />
        <Layer
          id="unit-range-edge"
          type="line"
          paint={{ "line-color": ink, "line-width": 1, "line-opacity": 0.5 }}
        />
      </Source>
      <Source id="unit-moves" type="geojson" data={lines}>
        <Layer
          id="unit-moves"
          type="line"
          paint={{ "line-color": ink, "line-width": 2, "line-dasharray": [2, 1.5] }}
        />
      </Source>
      {moves.map(({ placed, to }) => (
        <Marker
          key={placed.unit.id}
          longitude={to.longitude}
          latitude={to.latitude}
          anchor="center"
          style={{ pointerEvents: "none" }}
        >
          <span className={classes.ghost} aria-hidden>
            <UnitSymbol
              type={placed.unit.type}
              color={armyColorVar(placed.army.color)}
              width={30}
            />
          </span>
        </Marker>
      ))}
    </>
  );
}
