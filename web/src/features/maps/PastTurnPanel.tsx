import { Badge, Button, Group, List, Stack, Text } from "@mantine/core";
import { IconPlayerTrackNext } from "@tabler/icons-react";
import type {
  ArmySummary,
  ArmyTurnEventKind,
  ArmyTurnStatus,
  CampaignTurnSummary,
  ArmyUnitResponse,
} from "@/api/generated/model";
import { Section } from "@/components/Section";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import type { OpenArmyTurn } from "@/features/maps/use-orders";
import { formatDateTime } from "@/lib/format";
import { turnWhen } from "@/features/campaigns/calendar";

const statusLabels: Record<ArmyTurnStatus, string> = {
  Draft: "Draft",
  Submitted: "Submitted",
  Completed: "Approved",
};

const eventLabels: Record<ArmyTurnEventKind, string> = {
  Submitted: "Submitted",
  Approved: "Approved",
  SentBack: "Sent back",
  Reverted: "Reopened",
  Edited: "Orders changed",
};

interface PastTurnPanelProps {
  turn: CampaignTurnSummary;
  armies: readonly ArmySummary[];
  /** The armies' turns, for what happened to each in this one. */
  armyTurns: readonly OpenArmyTurn[];
  units: readonly ArmyUnitResponse[];
  /** The open turn's number, to go back to. */
  openTurn: number;
  onBack: () => void;
}

/**
 * A past turn (DESIGN.md §3.13): each army's part in it (the Umpire's, every army's; a
 * commander's, their own), with when it was submitted and approved and what happened on the way.
 */
export function PastTurnPanel({
  turn,
  armies,
  armyTurns,
  units,
  openTurn,
  onBack,
}: PastTurnPanelProps) {
  const unitName = (id: string) => units.find((u) => u.id === id)?.name ?? "A unit";

  return (
    <Section
      title={turn.number === 0 ? "Setup" : `Turn ${String(turn.number)}`}
      description={
        turn.number === 0
          ? "Where the Umpire placed the units."
          : `Where the units were after this turn${turnWhen(turn) ? ` (${turnWhen(turn) ?? ""})` : ""}.`
      }
    >
      <Stack gap="lg">
        {turn.armyTurns.map((summary) => {
          const army = armies.find((a) => a.id === summary.armyId);
          if (!army) return null;
          const history =
            armyTurns
              .find((entry) => entry.army.id === army.id)
              ?.turns.find((t) => t.turn === turn.number)?.history ?? [];
          return (
            <Stack key={army.id} gap={6}>
              <Group justify="space-between" wrap="nowrap">
                <Text fw={600} size="sm">
                  <ArmyBadge army={army} />
                </Text>
                <Badge variant="light" color="gray">
                  {statusLabels[summary.status]}
                </Badge>
              </Group>
              {history.length > 0 ? (
                <List size="xs" spacing={4} aria-label={`What happened to ${army.name}'s turn`}>
                  {history.map((event) => (
                    <List.Item key={`${event.kind}-${event.at}`}>
                      {eventLabels[event.kind]}
                      {event.byName && ` by ${event.byName}`}, {formatDateTime(event.at)}
                      {event.note && `: ${event.note}`}
                      {event.unitNotes.map((note) => (
                        <Text key={note.unitId} size="xs" c="dimmed">
                          {unitName(note.unitId)}: {note.text}
                        </Text>
                      ))}
                    </List.Item>
                  ))}
                </List>
              ) : (
                summary.completedAt && (
                  <Text size="xs" c="dimmed">
                    Approved {formatDateTime(summary.completedAt)}.
                  </Text>
                )
              )}
            </Stack>
          );
        })}
        <Button
          variant="default"
          leftSection={<IconPlayerTrackNext size={16} aria-hidden />}
          onClick={onBack}
        >
          Back to turn {openTurn}
        </Button>
      </Stack>
    </Section>
  );
}
