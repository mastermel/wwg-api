import { IconEye } from "@tabler/icons-react";
import { Marker } from "react-map-gl/maplibre";
import type { ArmySummary, SightingResponse } from "@/api/generated/model";
import { armyColors } from "@/features/armies/identity/army-colors";
import classes from "@/features/maps/SightingMarkers.module.css";

/**
 * Sightings with a hex, on the map inside a CampaignMap (step 49b): an eye in the sighted army's
 * colour (grey when its army wasn't told), faded for the turns after its own. For sight only: the
 * Sightings panel lists them.
 */
export function SightingMarkers({
  sightings,
  armies,
}: {
  sightings: readonly { sighting: SightingResponse; faded: boolean }[];
  armies: readonly ArmySummary[];
}) {
  return (
    <>
      {sightings.map(({ sighting, faded }) => {
        if (sighting.latitude === null || sighting.longitude === null) return null;
        const army = armies.find((a) => a.id === sighting.armyIds?.[0]);
        return (
          <Marker
            key={sighting.id}
            longitude={sighting.longitude}
            latitude={sighting.latitude}
            anchor="center"
            offset={[0, -18]}
            style={{ pointerEvents: "none" }}
          >
            <span
              className={classes.sighting}
              data-faded={faded}
              // The tile is white in both schemes: the light colours are the ones checked against it.
              style={{ borderColor: army ? armyColors[army.color].light : undefined }}
              aria-hidden
            >
              <IconEye size={16} />
            </span>
          </Marker>
        );
      })}
    </>
  );
}
