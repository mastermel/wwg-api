import { Alert, Badge, Button, Group, List, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { IconCheck, IconPlayerTrackNext, IconSend } from "@tabler/icons-react";
import { useState } from "react";
import type {
  ArmyTurnDetails,
  ArmyTurnStatus,
  CampaignTurnSummary,
  ArmyUnitResponse,
} from "@/api/generated/model";
import { Section } from "@/components/Section";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import type { DepotThreat, HexWarning, UnitInHex } from "@/features/maps/contact";
import { HexWarningsList } from "@/features/maps/HexWarningsList";
import type { TerrainIndex } from "@/features/maps/terrain";
import { ReviewModal } from "@/features/maps/ReviewModal";
import { StartTurnModal } from "@/features/maps/StartTurnModal";
import type { OpenArmyTurn } from "@/features/maps/use-orders";
import type { useReview } from "@/features/maps/use-review";
import { formatDateTime } from "@/lib/format";
import { useOnline } from "@/lib/use-online";
import { turnWhen } from "@/features/campaigns/calendar";

const statusLabels: Record<ArmyTurnStatus, string> = {
  Draft: "Draft",
  Submitted: "Submitted",
  Completed: "Approved",
};

interface ReviewPanelProps {
  campaignId: string;
  /** Where the orders as given leave the units, for sightings added by hand. */
  places: readonly UnitInHex[];
  open: CampaignTurnSummary;
  /** What stops the next turn starting. */
  problems: readonly string[];
  armyTurns: readonly OpenArmyTurn[];
  units: readonly ArmyUnitResponse[];
  review: ReturnType<typeof useReview>;
  /** Contact and concentration, by the orders as given (step 46). */
  warnings: readonly HexWarning[];
  threats: readonly DepotThreat[];
  terrain: TerrainIndex;
}

/**
 * The Umpire's open turn (DESIGN.md §3.13): each army's turn with its status and times; Submit
 * for it while it's a Draft (decision 0011), Approve or Send back once it's submitted, Reopen
 * once it's approved; and Start turn N+1 once every army's turn is approved.
 */
export function ReviewPanel({
  campaignId,
  places,
  open,
  problems,
  armyTurns,
  units,
  review,
  warnings,
  threats,
  terrain,
}: ReviewPanelProps) {
  const online = useOnline();
  const [reviewing, setReviewing] = useState<{
    kind: "send-back" | "revert";
    entry: OpenArmyTurn;
    turn: ArmyTurnDetails;
  } | null>(null);
  const [starting, { open: askToStart, close: closeStart }] = useDisclosure(false);
  const next = open.number + 1;

  return (
    <Section
      title={`Turn ${String(open.number)}`}
      description={[
        turnWhen(open) ? `${turnWhen(open) ?? ""}.` : null,
        `${String(open.submitted)} of ${String(open.armies)} armies have submitted this turn.`,
      ]
        .filter(Boolean)
        .join("\n")}
    >
      <Stack gap="lg">
        {armyTurns.map((entry) =>
          entry.turn ? (
            <ArmyTurnReview
              key={entry.army.id}
              entry={entry}
              turn={entry.turn}
              review={review}
              onReview={(kind, turn) => {
                setReviewing({ kind, entry, turn });
              }}
            />
          ) : null,
        )}
        <HexWarningsList warnings={warnings} threats={threats} terrain={terrain} from="orders" />
        {problems.length > 0 && (
          <Alert role="status" color="gray" title={`Before turn ${String(next)} can start`}>
            <List size="sm" spacing={2}>
              {problems.map((problem) => (
                <List.Item key={problem}>{problem}</List.Item>
              ))}
            </List>
          </Alert>
        )}
        <Button
          leftSection={<IconPlayerTrackNext size={16} aria-hidden />}
          disabled={!online || problems.length > 0}
          onClick={askToStart}
        >
          Start turn {next}
        </Button>
      </Stack>
      {reviewing && (
        <ReviewModal
          kind={reviewing.kind}
          army={reviewing.entry.army}
          turn={reviewing.turn.turn}
          units={units.filter((u) => u.armyId === reviewing.entry.army.id)}
          onSubmit={(data) =>
            review.review(reviewing.kind, reviewing.turn, reviewing.entry.army.name, data)
          }
          onClose={() => {
            setReviewing(null);
          }}
        />
      )}
      {starting && (
        <StartTurnModal
          campaignId={campaignId}
          closing={open.number}
          armies={armyTurns.map((entry) => entry.army)}
          places={places}
          busy={review.busy}
          onStart={(attrition, sightings) => {
            void review.startNext(next, attrition, sightings).then((started) => {
              if (started) closeStart();
            });
          }}
          onClose={closeStart}
        />
      )}
    </Section>
  );
}

interface ArmyTurnReviewProps {
  entry: OpenArmyTurn;
  turn: ArmyTurnDetails;
  review: ReturnType<typeof useReview>;
  onReview: (kind: "send-back" | "revert", turn: ArmyTurnDetails) => void;
}

function ArmyTurnReview({ entry: { army }, turn, review, onReview }: ArmyTurnReviewProps) {
  const online = useOnline();
  const moves = turn.orders.filter((o) => o.kind === "Move").length;
  const holds = turn.orders.length - moves;
  const commander = army.commander
    ? `${army.commander.firstName} ${army.commander.lastName}`
    : null;

  return (
    <Stack gap={6}>
      <Group justify="space-between" wrap="nowrap">
        <Text fw={600} size="sm">
          <ArmyBadge army={army} />
        </Text>
        <Badge variant="light" color={turn.status === "Draft" ? "gray" : "navy"}>
          {statusLabels[turn.status]}
        </Badge>
      </Group>
      <Text size="xs" c="dimmed">
        {turn.status === "Draft"
          ? `${commander ? `${commander} is giving orders` : "No commander: give its orders on the map"} (${String(turn.orders.length)} so far).`
          : `${String(moves)} ${moves === 1 ? "move" : "moves"}, ${String(holds)} ${holds === 1 ? "hold" : "holds"}. ` +
            (turn.status === "Submitted" && turn.submittedAt
              ? `Submitted ${formatDateTime(turn.submittedAt)}.`
              : turn.completedAt
                ? `Approved ${formatDateTime(turn.completedAt)}.`
                : "")}
      </Text>
      {turn.status === "Draft" && (
        <Group gap="xs">
          <Button
            size="compact-sm"
            variant="default"
            leftSection={<IconSend size={14} aria-hidden />}
            aria-label={`Submit ${army.name}'s turn for it`}
            disabled={!online || review.busy}
            onClick={() => void review.submit(turn, army.name)}
          >
            Submit for them
          </Button>
        </Group>
      )}
      {turn.status === "Submitted" && (
        <Group gap="xs">
          <Button
            size="compact-sm"
            leftSection={<IconCheck size={14} aria-hidden />}
            aria-label={`Approve ${army.name}'s turn`}
            disabled={!online || review.busy}
            onClick={() => void review.approve(turn, army.name)}
          >
            Approve
          </Button>
          <Button
            size="compact-sm"
            variant="default"
            aria-label={`Send back ${army.name}'s turn`}
            disabled={!online}
            onClick={() => {
              onReview("send-back", turn);
            }}
          >
            Send back
          </Button>
        </Group>
      )}
      {turn.status === "Completed" && (
        <Group gap="xs">
          <Button
            size="compact-sm"
            variant="subtle"
            aria-label={`Reopen ${army.name}'s turn`}
            disabled={!online}
            onClick={() => {
              onReview("revert", turn);
            }}
          >
            Reopen
          </Button>
        </Group>
      )}
    </Stack>
  );
}
