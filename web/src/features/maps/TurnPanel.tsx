import { ActionIcon, Alert, Badge, Button, Group, List, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { IconArrowBackUp, IconSend } from "@tabler/icons-react";
import { useState } from "react";
import type { ArmyTurnDetails, ArmyTurnStatus, CampaignTurnSummary } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Section } from "@/components/Section";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import { armyColorVar } from "@/features/armies/identity/army-colors";
import { describeOrder } from "@/features/maps/orders";
import type { PlacedUnit } from "@/features/maps/stacks";
import { reviewOf, type OpenArmyTurn, type useOrders } from "@/features/maps/use-orders";
import { UnitSymbol } from "@/features/units/UnitSymbol";
import { useOnline } from "@/lib/use-online";

/** A commander's unit, and where it is (undefined: the Umpire hasn't placed it yet). */
export interface CommandedUnit {
  unit: PlacedUnit["unit"];
  army: PlacedUnit["army"];
  placed: PlacedUnit | undefined;
}

const statusLabels: Record<ArmyTurnStatus, string> = {
  Draft: "Draft",
  Submitted: "Submitted",
  Completed: "Approved",
};

interface TurnPanelProps {
  /** The open campaign turn, for its number and how far it's got. */
  open: CampaignTurnSummary;
  commanded: readonly OpenArmyTurn[];
  units: readonly CommandedUnit[];
  orders: ReturnType<typeof useOrders>;
  /** Opens a unit on the map (its drawer, with Move and Hold). */
  onChoose: (placed: PlacedUnit) => void;
}

/**
 * A commander's turn (DESIGN.md §3.13): how far the turn has got, and for each army they command
 * its status, the Umpire's notes, each unit's order (with undo) and Submit.
 */
export function TurnPanel({ open, commanded, units, orders, onChoose }: TurnPanelProps) {
  const [submitting, setSubmitting] = useState<OpenArmyTurn | null>(null);
  const [confirming, { open: ask, close }] = useDisclosure(false);

  return (
    <Section
      title={`Turn ${String(open.number)}`}
      description={`${String(open.submitted)} of ${String(open.armies)} armies have submitted this turn.`}
    >
      <Stack gap="lg">
        {commanded.map((entry) =>
          entry.turn ? (
            <ArmyTurnOrders
              key={entry.army.id}
              entry={entry}
              turn={entry.turn}
              units={units.filter((u) => u.army.id === entry.army.id)}
              orders={orders}
              onChoose={onChoose}
              onSubmit={() => {
                setSubmitting(entry);
                ask();
              }}
            />
          ) : null,
        )}
      </Stack>
      <ConfirmModal
        opened={confirming}
        onClose={close}
        title="Submit this turn?"
        confirmLabel="Submit"
        color="navy"
        loading={orders.busy}
        onConfirm={() => {
          const turn = submitting?.turn;
          if (submitting && turn) {
            void orders.submit(submitting.army.id, turn, submitting.army.name).then(close);
          }
        }}
      >
        The Umpire reviews {submitting?.army.name}&apos;s orders next. They can&apos;t be changed
        once submitted, unless the Umpire sends them back.
      </ConfirmModal>
    </Section>
  );
}

interface ArmyTurnOrdersProps {
  entry: OpenArmyTurn;
  turn: ArmyTurnDetails;
  units: readonly CommandedUnit[];
  orders: ReturnType<typeof useOrders>;
  onChoose: (placed: PlacedUnit) => void;
  onSubmit: () => void;
}

function ArmyTurnOrders({
  entry: { army },
  turn,
  units,
  orders,
  onChoose,
  onSubmit,
}: ArmyTurnOrdersProps) {
  const online = useOnline();
  const draft = turn.status === "Draft";
  const review = reviewOf(turn);
  const orderOf = (unitId: string) => turn.orders.find((o) => o.unitId === unitId);
  const missing = units.filter((u) => u.placed && !orderOf(u.unit.id)).length;

  return (
    <Stack gap="xs">
      <Group justify="space-between" wrap="nowrap">
        <Text fw={600} size="sm">
          <ArmyBadge army={army} />
        </Text>
        <Badge variant="light" color={draft ? "gray" : "navy"}>
          {statusLabels[turn.status]}
        </Badge>
      </Group>
      {review && (
        <Alert
          role="status"
          color="yellow"
          title={review.kind === "SentBack" ? "Sent back" : "Reopened"}
        >
          <Text size="sm">
            {review.byName ?? "The Umpire"}{" "}
            {review.kind === "SentBack" ? "sent this turn back" : "reopened this turn"}
            {review.note ? `: ${review.note}` : "."}
          </Text>
        </Alert>
      )}
      <List listStyleType="none" spacing={6} aria-label={`${army.name}'s orders`}>
        {units.map(({ unit, placed }) => {
          const order = orderOf(unit.id);
          const note = review?.unitNotes.find((n) => n.unitId === unit.id);
          return (
            <List.Item key={unit.id}>
              <Group justify="space-between" wrap="nowrap" gap="xs">
                <Group gap="xs" wrap="nowrap" miw={0}>
                  <UnitSymbol type={unit.type} color={armyColorVar(army.color)} width={26} />
                  <div style={{ minWidth: 0 }}>
                    {placed && draft ? (
                      <Button
                        variant="transparent"
                        size="compact-sm"
                        px={0}
                        onClick={() => {
                          onChoose(placed);
                        }}
                      >
                        {unit.name}
                      </Button>
                    ) : (
                      <Text size="sm" truncate>
                        {unit.name}
                      </Text>
                    )}
                    <Text size="xs" c="dimmed">
                      {describeOrder(order, placed)}
                      {order?.byUmpire && " · set by the Umpire"}
                    </Text>
                    {note && (
                      <Text size="xs" fw={500}>
                        Umpire: {note.text}
                      </Text>
                    )}
                  </div>
                </Group>
                {draft && order && (
                  <ActionIcon
                    variant="subtle"
                    color="gray"
                    aria-label={`Undo ${unit.name}'s order`}
                    disabled={!online || orders.busy}
                    onClick={() => void orders.undo(army.id, turn.id, unit)}
                  >
                    <IconArrowBackUp size={18} aria-hidden />
                  </ActionIcon>
                )}
              </Group>
            </List.Item>
          );
        })}
      </List>
      {draft ? (
        <>
          <Button
            leftSection={<IconSend size={16} aria-hidden />}
            disabled={!online || missing > 0}
            onClick={onSubmit}
          >
            Submit turn {turn.turn}
          </Button>
          {missing > 0 && (
            <Text size="xs" c="dimmed">
              Give every unit an order to submit ({missing} to go). Choose a unit on the map or
              here.
            </Text>
          )}
        </>
      ) : (
        <Text size="sm" c="dimmed">
          {turn.status === "Submitted"
            ? "Submitted: the Umpire reviews it next."
            : "Approved: the next turn starts once every army's is."}
        </Text>
      )}
    </Stack>
  );
}
