import { Button, Group, Stack, Switch, Text } from "@mantine/core";
import {
  IconAnchor,
  IconArrowBackUp,
  IconArrowMoveRight,
  IconHammer,
  IconHandStop,
  IconSailboat,
} from "@tabler/icons-react";
import type { ArmyTurnDetails } from "@/api/generated/model";
import { boatCount } from "@/features/maps/boats";
import { describeOrder } from "@/features/maps/orders";
import type { PlacedUnit } from "@/features/maps/stacks";
import { reviewOf } from "@/features/maps/use-orders";
import { useOnline } from "@/lib/use-online";

interface OrderActionsProps {
  placed: PlacedUnit;
  /** The unit's army's turn in the open campaign turn. */
  turn: ArmyTurnDetails;
  /** Whether the viewer can change its orders now (a commander: a Draft; the Umpire: or Submitted). */
  editable: boolean;
  busy: boolean;
  onMove: () => void;
  onHold: () => void;
  onUndo: () => void;
  /**
   * Living off the land (step 48b, decision 0019), for a unit whose nation may: whether it does
   * this turn, and the switch's change.
   */
  offTheLand?: { on: boolean; onChange: (on: boolean) => void };
  /** What the unit can do with boats (step 51, decision 0022). */
  boats?: BoatOptions;
}

/** A unit's options with boats: on them, boarding them, or building one. */
export interface BoatOptions {
  /** The boats it's on (none ashore). */
  aboard: number;
  /** Ashore, by its army's free boats: how many it needs and how many there are (none: no option). */
  embark?: { needed: number; free: number };
  /** Whether it's in a settlement on a waterway, where boats are built (ashore only). */
  canBuild: boolean;
  /** For the Umpire: it has more points than its boats carry, as the capacity now is. */
  overloaded?: string;
  onEmbark: () => void;
  onLand: () => void;
  onBuild: () => void;
}

/**
 * In the unit drawer, for the unit's commander or the Umpire: its order this turn (and whether
 * the Umpire set it), the Umpire's note, and Move, Hold and Take back while it can change.
 */
export function OrderActions({
  placed,
  turn,
  editable,
  busy,
  onMove,
  onHold,
  onUndo,
  offTheLand,
  boats,
}: OrderActionsProps) {
  const online = useOnline();
  const order = turn.orders.find((o) => o.unitId === placed.unit.id);
  const note = reviewOf(turn)?.unitNotes.find((n) => n.unitId === placed.unit.id);

  return (
    <Stack gap="xs">
      <Text size="sm">
        Turn {turn.turn}: {describeOrder(order, placed)}
        {order?.byUmpire && " (set by the Umpire)"}
      </Text>
      {note && (
        <Text size="sm" fw={500}>
          Umpire: {note.text}
        </Text>
      )}
      {boats && boats.aboard > 0 && (
        <Text size="sm">
          On {boatCount(boats.aboard)}: it moves as boats do, and its boats go with it.
        </Text>
      )}
      {boats?.overloaded && (
        <Text size="sm" fw={500} c="red">
          {boats.overloaded}
        </Text>
      )}
      {offTheLand && (
        <Switch
          label="Living off the land"
          description="Never out of supply, but its side's hex is held to half the concentration."
          checked={offTheLand.on}
          disabled={!editable || !online || busy}
          onChange={(event) => {
            offTheLand.onChange(event.currentTarget.checked);
          }}
        />
      )}
      {editable ? (
        <Group grow>
          <Button
            variant="light"
            leftSection={<IconArrowMoveRight size={16} aria-hidden />}
            disabled={!online || busy}
            onClick={onMove}
          >
            Move
          </Button>
          <Button
            variant="default"
            leftSection={<IconHandStop size={16} aria-hidden />}
            disabled={!online || busy}
            onClick={onHold}
          >
            Hold
          </Button>
          {boats && boats.aboard > 0 && (
            <Button
              variant="default"
              leftSection={<IconAnchor size={16} aria-hidden />}
              disabled={!online || busy}
              onClick={boats.onLand}
            >
              Land
            </Button>
          )}
          {order && (
            <Button
              variant="subtle"
              leftSection={<IconArrowBackUp size={16} aria-hidden />}
              disabled={!online || busy}
              onClick={onUndo}
            >
              Take back
            </Button>
          )}
        </Group>
      ) : null}
      {editable && boats?.aboard === 0 && (boats.embark !== undefined || boats.canBuild) && (
        <Stack gap={4}>
          <Group grow>
            {boats.embark && (
              <Button
                variant="default"
                leftSection={<IconSailboat size={16} aria-hidden />}
                disabled={!online || busy || boats.embark.free < boats.embark.needed}
                onClick={boats.onEmbark}
              >
                Embark
              </Button>
            )}
            {boats.canBuild && (
              <Button
                variant="default"
                leftSection={<IconHammer size={16} aria-hidden />}
                disabled={!online || busy}
                onClick={boats.onBuild}
              >
                Build a boat
              </Button>
            )}
          </Group>
          {boats.embark && (
            <Text size="xs" c="dimmed">
              It needs {boatCount(boats.embark.needed)}; {String(boats.embark.free)} free here.
              Embarking takes the turn.
            </Text>
          )}
          {boats.canBuild && (
            <Text size="xs" c="dimmed">
              Two turns in a row here build one.
            </Text>
          )}
        </Stack>
      )}
      {editable ? null : (
        <Text size="sm" c="dimmed">
          {turn.status === "Submitted"
            ? "Submitted: orders can't change unless the Umpire sends them back."
            : "Approved for this turn."}
        </Text>
      )}
    </Stack>
  );
}
