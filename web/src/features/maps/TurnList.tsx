import { Stack, Text, UnstyledButton } from "@mantine/core";
import { useRef, type KeyboardEvent } from "react";
import type { ArmyTurnStatus, CampaignTurnsResponse } from "@/api/generated/model";
import { Section } from "@/components/Section";
import classes from "@/features/maps/PanelList.module.css";

const statusLabels: Record<ArmyTurnStatus, string> = {
  Draft: "draft",
  Submitted: "submitted",
  Completed: "approved",
};

interface TurnListProps {
  turns: CampaignTurnsResponse;
  /** The turn shown on the map. */
  viewing: number;
  onView: (turn: number) => void;
  /** The Umpire (every army's progress), or a commander (their own army's status). */
  manager: boolean;
}

/**
 * The campaign's turns, newest first (DESIGN.md §3.13): choosing one shows the units where they
 * were after it. The arrow keys step through them: up and right to newer, down and left to older.
 */
export function TurnList({ turns, viewing, onView, manager }: TurnListProps) {
  const buttons = useRef(new Map<number, HTMLButtonElement>());
  const newestFirst = [...turns.turns].reverse();

  const step = (event: KeyboardEvent, number: number) => {
    const by = { ArrowUp: 1, ArrowRight: 1, ArrowDown: -1, ArrowLeft: -1 }[event.key];
    if (by === undefined) return;
    event.preventDefault();
    const next = Math.min(turns.openTurn, Math.max(0, number + by));
    onView(next);
    buttons.current.get(next)?.focus();
  };

  return (
    <Section title="Turns" description="Choose one to see where the units were after it.">
      <Stack component="ul" gap={2} p={0} m={0} aria-label="Turns">
        {newestFirst.map((turn) => {
          const open = turn.closedAt === null;
          const detail =
            turn.number === 0
              ? "Setup"
              : manager
                ? `${String(turn.submitted)} of ${String(turn.armies)} submitted`
                : turn.armyTurns.map((a) => statusLabels[a.status]).join(", ") || "—";
          return (
            <li key={turn.number} style={{ listStyle: "none" }}>
              <UnstyledButton
                ref={(element) => {
                  if (element) buttons.current.set(turn.number, element);
                  else buttons.current.delete(turn.number);
                }}
                className={classes.row}
                aria-label={`Turn ${String(turn.number)}${open ? " (open)" : ""}: ${detail}`}
                aria-pressed={turn.number === viewing}
                onClick={() => {
                  onView(turn.number);
                }}
                onKeyDown={(event) => {
                  step(event, turn.number);
                }}
              >
                <Text size="sm" fw={500} component="span">
                  Turn {turn.number}
                  {open && " (open)"}
                </Text>
                <Text size="xs" c={turn.number === viewing ? undefined : "dimmed"} component="span">
                  {detail}
                </Text>
              </UnstyledButton>
            </li>
          );
        })}
      </Stack>
    </Section>
  );
}
