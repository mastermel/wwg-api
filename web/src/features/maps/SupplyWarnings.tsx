import { Alert, List } from "@mantine/core";
import { IconTruckOff } from "@tabler/icons-react";
import type {
  ArmyUnitResponse,
  CampaignSupplyResponse,
  DepotResponse,
} from "@/api/generated/model";
import { graceTurns, outOfSupplyNext } from "@/features/maps/supply";

interface SupplyWarningsProps {
  supply: CampaignSupplyResponse | undefined;
  units: readonly ArmyUnitResponse[];
  depots: readonly DepotResponse[];
  /** The armies to warn about: a commander's own; the Umpire's, every army. */
  armyIds: readonly string[];
}

/**
 * The turn panel's supply warnings (step 48d): units the orders as given leave out of supply, and
 * intermediate depots cut off from their main ones. Nothing when there's none.
 */
export function SupplyWarnings({ supply, units, depots, armyIds }: SupplyWarningsProps) {
  const cutOff =
    supply?.units.filter((s) => armyIds.includes(s.armyId) && outOfSupplyNext(s)) ?? [];
  const isolated =
    supply?.depots.filter(
      (d) => !d.connected && armyIds.includes(depots.find((x) => x.id === d.depotId)?.armyId ?? ""),
    ) ?? [];
  if (cutOff.length === 0 && isolated.length === 0) return null;

  return (
    <Alert color="orange" icon={<IconTruckOff aria-hidden />} title="Supply">
      <List size="sm" spacing={4} aria-label="Supply">
        {cutOff.map((s) => {
          const name = units.find((u) => u.id === s.unitId)?.name ?? "A unit";
          const turn = s.nextUnsuppliedTurns;
          return (
            <List.Item key={s.unitId}>
              {name}: out of supply after this turn (
              {turn > graceTurns
                ? `its ${String(turn)}th turn: attrition`
                : `${String(turn)} of ${String(graceTurns)} turns before attrition`}
              ).
            </List.Item>
          );
        })}
        {isolated.map((d) => {
          const depot = depots.find((x) => x.id === d.depotId);
          return (
            <List.Item key={d.depotId}>
              {depot?.name ?? "An intermediate depot"} is cut off from its main depots:{" "}
              {String(d.cutOffTurns)} of 15 turns&apos; stock used.
            </List.Item>
          );
        })}
      </List>
    </Alert>
  );
}
