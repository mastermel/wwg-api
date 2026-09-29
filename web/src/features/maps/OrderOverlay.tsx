import { useComputedColorScheme } from "@mantine/core";
import type { FeatureCollection, LineString, Polygon } from "geojson";
import { Layer, Marker, Source } from "react-map-gl/maplibre";
import { armyColorVar } from "@/features/armies/identity/army-colors";
import { circle, type Point } from "@/features/maps/geo";
import { mapPalettes } from "@/features/maps/map-style";
import classes from "@/features/maps/OrderOverlay.module.css";
import type { PlacedUnit } from "@/features/maps/stacks";
import { UnitSymbol } from "@/features/units/UnitSymbol";

/** A unit's move this turn, not yet approved: from where it is to `to`. */
export interface PendingMove {
  placed: PlacedUnit;
  to: Point;
}

interface OrderOverlayProps {
  moves: readonly PendingMove[];
  /** The range of the unit being moved: a circle of `metres` around `centre`. */
  range?: { centre: Point; metres: number };
}

/**
 * Moves not yet approved (DESIGN.md §3.13): a ghost of each unit where it's going, with a dashed
 * line from where it is; and while moving one, its range. Inside a CampaignMap. Drawn for sight
 * only: the turn panel lists the same orders for screen readers and keyboards.
 */
export function OrderOverlay({ moves, range }: OrderOverlayProps) {
  const ink = mapPalettes[useComputedColorScheme("light")].label;
  const lines: FeatureCollection<LineString> = {
    type: "FeatureCollection",
    features: moves.map(({ placed, to }) => ({
      type: "Feature",
      properties: {},
      geometry: {
        type: "LineString",
        coordinates: [
          [placed.longitude, placed.latitude],
          [to.longitude, to.latitude],
        ],
      },
    })),
  };
  const area: FeatureCollection<Polygon> = {
    type: "FeatureCollection",
    features: range
      ? [
          {
            type: "Feature",
            properties: {},
            geometry: { type: "Polygon", coordinates: [circle(range.centre, range.metres)] },
          },
        ]
      : [],
  };

  return (
    <>
      <Source id="unit-range" type="geojson" data={area}>
        <Layer
          id="unit-range-fill"
          type="fill"
          paint={{ "fill-color": ink, "fill-opacity": 0.08 }}
        />
        <Layer
          id="unit-range-edge"
          type="line"
          paint={{ "line-color": ink, "line-width": 2, "line-dasharray": [1, 1] }}
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
