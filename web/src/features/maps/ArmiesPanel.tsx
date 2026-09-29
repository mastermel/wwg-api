import { Stack, Text, UnstyledButton } from "@mantine/core";
import type { ArmySummary } from "@/api/generated/model";
import { Section } from "@/components/Section";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import classes from "@/features/maps/PanelList.module.css";

interface ArmiesPanelProps {
  armies: readonly ArmySummary[];
  /** How many of each army's units are on the map, by army ID. */
  onMap: ReadonlyMap<string, number>;
  /** The army whose units stand out, if one does. */
  highlighted: string | null;
  onHighlight: (armyId: string | null) => void;
}

/**
 * The Umpire's armies (DESIGN.md §3.13): choosing one highlights its units on the map and fades
 * the rest; choosing it again shows them all alike.
 */
export function ArmiesPanel({ armies, onMap, highlighted, onHighlight }: ArmiesPanelProps) {
  return (
    <Section title="Armies" description="Choose one to pick out its units on the map.">
      <Stack component="ul" gap={2} p={0} m={0} aria-label="Armies">
        {armies.map((army) => {
          const count = onMap.get(army.id) ?? 0;
          const commander = army.commander
            ? `${army.commander.firstName} ${army.commander.lastName}`
            : "No commander";
          return (
            <li key={army.id} style={{ listStyle: "none" }}>
              <UnstyledButton
                className={classes.row}
                aria-label={`${army.name}: ${commander}, ${String(count)} on the map`}
                aria-pressed={army.id === highlighted}
                onClick={() => {
                  onHighlight(army.id === highlighted ? null : army.id);
                }}
              >
                <Text size="sm" component="span" miw={0}>
                  <ArmyBadge army={army} />
                </Text>
                <Text size="xs" c={army.id === highlighted ? undefined : "dimmed"} component="span">
                  {count} on the map
                </Text>
              </UnstyledButton>
            </li>
          );
        })}
      </Stack>
    </Section>
  );
}
