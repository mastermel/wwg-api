import { IconFlagFilled } from "@tabler/icons-react";
import { Marker } from "react-map-gl/maplibre";
import type { ArmySummary, SettlementScoreResponse } from "@/api/generated/model";
import { armyColors } from "@/features/armies/identity/army-colors";
import classes from "@/features/maps/HoldingFlags.module.css";

/**
 * Who holds each settlement the viewer may know of (step 50), on the map inside a CampaignMap: a
 * flag in the holding army's colour beside it. For sight only: the scoreboard lists them.
 */
export function HoldingFlags({
  settlements,
  armies,
}: {
  settlements: readonly SettlementScoreResponse[];
  armies: readonly ArmySummary[];
}) {
  return (
    <>
      {settlements.map((s) => {
        const army = armies.find((a) => a.id === s.armyId);
        if (!army) return null;
        return (
          <Marker
            key={`${String(s.q)},${String(s.r)}`}
            longitude={s.longitude}
            latitude={s.latitude}
            anchor="bottom-left"
            offset={[6, -4]}
            style={{ pointerEvents: "none" }}
          >
            {/* The tile is white in both schemes: the light colours are the ones checked against it. */}
            <span className={classes.flag} aria-hidden>
              <IconFlagFilled size={14} color={armyColors[army.color].light} />
            </span>
          </Marker>
        );
      })}
    </>
  );
}
