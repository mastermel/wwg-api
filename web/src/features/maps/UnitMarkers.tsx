import { useEffect, useState } from "react";
import { Marker, useMap } from "react-map-gl/maplibre";
import { armyColorVar } from "@/features/armies/identity/army-colors";
import { describeUnit, stackUnits, type PlacedUnit, type UnitStack } from "@/features/maps/stacks";
import classes from "@/features/maps/UnitMarkers.module.css";
import { UnitSymbol } from "@/features/units/UnitSymbol";

interface UnitMarkersProps {
  units: readonly PlacedUnit[];
  onSelect: (stack: UnitStack) => void;
  /** An army to pick out: stacks without any of its units are faded. */
  highlight?: string | null;
  /** Units out of supply (step 48d), marked as such, for those who see their supply. */
  outOfSupply?: ReadonlySet<string>;
}

/**
 * The units on the map, as buttons: one per unit, or one per stack where they'd overlap, which
 * shows how many and opens the list to choose from (DESIGN.md §3.13). Restacked as the zoom
 * changes. Inside a CampaignMap.
 */
export function UnitMarkers({ units, onSelect, highlight, outOfSupply }: UnitMarkersProps) {
  const { current: map } = useMap();
  const [stacks, setStacks] = useState<UnitStack[]>([]);

  // Restacked when the units change and on zoom (the distances in pixels change); panning moves
  // every marker together.
  useEffect(() => {
    if (!map) return;
    const restack = () => {
      setStacks(stackUnits(units, (lngLat) => map.project(lngLat)));
    };
    restack();
    map.on("zoomend", restack);
    return () => {
      map.off("zoomend", restack);
    };
  }, [map, units]);

  return stacks.map((stack) => {
    const [top, ...under] = stack.units as [PlacedUnit, ...PlacedUnit[]];
    const next = under.at(0);
    const cutOff = stack.units.filter((u) => outOfSupply?.has(u.unit.id));
    const label =
      (under.length === 0
        ? describeUnit(top)
        : `${String(stack.units.length)} units: ${stack.units.map((u) => u.unit.name).join(", ")}`) +
      (cutOff.length === 0
        ? ""
        : under.length === 0
          ? ", out of supply"
          : `; out of supply: ${cutOff.map((u) => u.unit.name).join(", ")}`);
    return (
      <Marker
        key={stack.key}
        longitude={stack.longitude}
        latitude={stack.latitude}
        anchor="center"
        onClick={(event) => {
          event.originalEvent.stopPropagation();
        }}
      >
        <button
          type="button"
          className={classes.marker}
          data-faded={highlight ? !stack.units.some((u) => u.army.id === highlight) : undefined}
          aria-label={label}
          title={label}
          onClick={() => {
            onSelect(stack);
          }}
        >
          {next && (
            <span className={classes.under} aria-hidden>
              <UnitSymbol type={next.unit.type} color={armyColorVar(next.army.color)} width={30} />
            </span>
          )}
          <span style={{ position: "relative", display: "block" }}>
            <UnitSymbol type={top.unit.type} color={armyColorVar(top.army.color)} width={30} />
          </span>
          {cutOff.length > 0 && (
            <span className={classes.supply} aria-hidden>
              !
            </span>
          )}
          {under.length > 0 && (
            <span className={classes.count} aria-hidden>
              {stack.units.length}
            </span>
          )}
        </button>
      </Marker>
    );
  });
}
