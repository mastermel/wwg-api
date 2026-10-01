import {
  Alert,
  Button,
  Group,
  Loader,
  Modal,
  NumberInput,
  Stack,
  Table,
  Text,
} from "@mantine/core";
import { useState } from "react";
import { useListAttritionDue } from "@/api/generated/endpoints/turns/turns";
import type {
  ArmySummary,
  AttritionDueResponse,
  AttritionLossRequest,
} from "@/api/generated/model";
import { attritionInWords, forcedMarchTurn } from "@/features/maps/marches";
import { graceTurns } from "@/features/maps/supply";

/**
 * Why a unit owes attrition, in words: its forced march (doubled out of supply), and from its 7th
 * turn out of supply, that too (steps 47 and 48).
 */
function whyItOwes(d: AttritionDueResponse) {
  const out = d.unsuppliedTurns > 0;
  return [
    d.forcedMarchMultiplier > 0
      ? `${forcedMarchTurn(d.forcedMarchTurns)}, ${attritionInWords(d.forcedMarchMultiplier) ?? ""}${out ? " (doubled: out of supply)" : ""}`
      : null,
    d.unsuppliedTurns > graceTurns
      ? `out of supply ${String(d.unsuppliedTurns)} turns, normal attrition`
      : null,
  ]
    .filter(Boolean)
    .join("; ");
}

interface StartTurnModalProps {
  campaignId: string;
  /** The turn closing. */
  closing: number;
  armies: readonly ArmySummary[];
  busy: boolean;
  onStart: (attrition: AttritionLossRequest[]) => void;
  onClose: () => void;
}

/**
 * Starting the next turn (DESIGN.md §3.13), with the attrition the closing turn's forced marches
 * cost (step 47, decision 0018): each unit's loss, which the Umpire can change before confirming.
 * Mount it only while open.
 */
export function StartTurnModal({
  campaignId,
  closing,
  armies,
  busy,
  onStart,
  onClose,
}: StartTurnModalProps) {
  const next = closing + 1;
  // Afresh each time: orders may have changed since.
  const due = useListAttritionDue(campaignId, {
    query: { meta: { persist: false }, refetchOnMount: "always" },
  });

  return (
    <Modal opened onClose={onClose} title={`Start turn ${String(next)}?`} centered size="lg">
      <Stack gap="md">
        <Text size="sm">
          Turn {closing} closes: its approved orders become where every unit is, and no turn in it
          can be reopened. Every commander is emailed to give orders for turn {next}.
        </Text>
        {due.isPending ? (
          <Group gap="xs">
            <Loader size="sm" />
            <Text size="sm">Working out the attrition…</Text>
          </Group>
        ) : due.isError ? (
          <Alert color="red" role="alert">
            The attrition couldn&apos;t be worked out. Close this and try again.
          </Alert>
        ) : (
          <AttritionForm
            key={JSON.stringify(due.data)}
            due={due.data}
            armies={armies}
            next={next}
            busy={busy}
            onStart={onStart}
            onClose={onClose}
          />
        )}
      </Stack>
    </Modal>
  );
}

function AttritionForm({
  due,
  armies,
  next,
  busy,
  onStart,
  onClose,
}: {
  due: readonly AttritionDueResponse[];
  armies: readonly ArmySummary[];
  next: number;
  busy: boolean;
  onStart: (attrition: AttritionLossRequest[]) => void;
  onClose: () => void;
}) {
  const [losses, setLosses] = useState<Record<string, number | undefined>>(() =>
    Object.fromEntries(due.map((d) => [d.unitId, d.loss])),
  );
  const complete = due.every((d) => losses[d.unitId] !== undefined);

  return (
    <Stack gap="md">
      {due.length > 0 && (
        <Stack gap="xs">
          <Text size="sm" fw={600}>
            Attrition from forced marches
          </Text>
          <Text size="xs" c="dimmed">
            Points lost by the rules&apos; scale for each unit&apos;s FF and size. Change any before
            starting; what&apos;s left of a point is carried to its next.
          </Text>
          <Table layout="fixed" verticalSpacing="xs">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Unit</Table.Th>
                <Table.Th w={110}>Points lost</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {due.map((d) => (
                <Table.Tr key={d.unitId}>
                  <Table.Td>
                    <Text size="sm" fw={500}>
                      {d.name}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {armies.find((a) => a.id === d.armyId)?.name ?? "An army"}: FF{" "}
                      {d.fightingFactor}, {d.points} points; {whyItOwes(d)}.
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <NumberInput
                      aria-label={`Points ${d.name} loses`}
                      min={0}
                      max={d.points}
                      allowDecimal={false}
                      allowNegative={false}
                      clampBehavior="strict"
                      // Its step buttons have no accessible names; arrow keys still step.
                      hideControls
                      value={losses[d.unitId] ?? ""}
                      onChange={(value) => {
                        setLosses((now) => ({
                          ...now,
                          [d.unitId]: typeof value === "number" ? value : undefined,
                        }));
                      }}
                    />
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Stack>
      )}
      <Group justify="flex-end">
        <Button variant="default" onClick={onClose}>
          Cancel
        </Button>
        <Button
          color="navy"
          loading={busy}
          disabled={!complete}
          onClick={() => {
            onStart(due.map((d) => ({ unitId: d.unitId, points: losses[d.unitId] ?? 0 })));
          }}
        >
          Start turn {next}
        </Button>
      </Group>
    </Stack>
  );
}
