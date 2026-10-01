import { Marker } from "react-map-gl/maplibre";
import type { ArmySummary, ReportResponse } from "@/api/generated/model";
import { armyColorVar } from "@/features/armies/identity/army-colors";
import { UnitSymbol } from "@/features/units/UnitSymbol";

/**
 * A received report's snapshot on the map, inside a CampaignMap (step 49d): the ally's units where
 * they were when it was sent, faded as past positions are. For sight only: the report says so.
 */
export function SnapshotMarkers({
  report,
  armies,
}: {
  report: ReportResponse | undefined;
  armies: readonly ArmySummary[];
}) {
  const army = armies.find((a) => a.id === report?.fromArmyId);
  return (
    <>
      {(report?.snapshot ?? []).map((unit, index) => (
        <Marker
          key={`${unit.name}-${String(index)}`}
          longitude={unit.longitude}
          latitude={unit.latitude}
          anchor="center"
          style={{ pointerEvents: "none", opacity: 0.55 }}
        >
          <span aria-hidden>
            <UnitSymbol
              type={unit.type}
              color={army ? armyColorVar(army.color) : "var(--mantine-color-silver-3)"}
              width={26}
            />
          </span>
        </Marker>
      ))}
    </>
  );
}
