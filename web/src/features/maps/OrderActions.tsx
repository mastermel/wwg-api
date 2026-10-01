import { Button, Group, Stack, Switch, Text } from "@mantine/core";
import { IconArrowBackUp, IconArrowMoveRight, IconHandStop } from "@tabler/icons-react";
import type { ArmyTurnDetails } from "@/api/generated/model";
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
      ) : (
        <Text size="sm" c="dimmed">
          {turn.status === "Submitted"
            ? "Submitted: orders can't change unless the Umpire sends them back."
            : "Approved for this turn."}
        </Text>
      )}
    </Stack>
  );
}
