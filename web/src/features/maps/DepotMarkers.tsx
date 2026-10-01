import { IconBuildingWarehouse } from "@tabler/icons-react";
import { Marker } from "react-map-gl/maplibre";
import type { ArmySummary, DepotResponse } from "@/api/generated/model";
import { armyColors } from "@/features/armies/identity/army-colors";
import classes from "@/features/maps/DepotMarkers.module.css";

/**
 * The depots the viewer may see (step 48a), on the map inside a CampaignMap: a storehouse in its
 * army's colour, dashed for an intermediate one. For sight only: the Depots panel lists them.
 */
export function DepotMarkers({
  depots,
  armies,
}: {
  depots: readonly DepotResponse[];
  armies: readonly ArmySummary[];
}) {
  return (
    <>
      {depots.map((depot) => {
        const army = armies.find((a) => a.id === depot.armyId);
        return (
          <Marker
            key={depot.id}
            longitude={depot.longitude}
            latitude={depot.latitude}
            anchor="center"
            offset={[0, 18]}
            style={{ pointerEvents: "none" }}
          >
            <span
              className={classes.depot}
              data-kind={depot.kind}
              // The tile is white in both schemes: the light colours are the ones checked against it.
              style={{ borderColor: army ? armyColors[army.color].light : undefined }}
              aria-hidden
            >
              <IconBuildingWarehouse size={16} />
            </span>
          </Marker>
        );
      })}
    </>
  );
}
