import { List, Text } from "@mantine/core";
import type { ArmySummary, SightingResponse } from "@/api/generated/model";
import { Section } from "@/components/Section";
import { describeSighting } from "@/features/maps/sightings";

interface SightingsPanelProps {
  /** The viewer's sightings (the Umpire's, every army's). */
  sightings: readonly SightingResponse[];
  armies: readonly ArmySummary[];
  /** The turn shown on the map. */
  turn: number;
  /** The Umpire sees every army's, under the army that saw them. */
  manager: boolean;
}

/**
 * The sightings made for the turn shown (step 49b, decision 0020), in words: what the Umpire
 * revealed of the other side. Nothing when there's none.
 */
export function SightingsPanel({ sightings, armies, turn, manager }: SightingsPanelProps) {
  const shown = sightings.filter((s) => s.turn === turn);
  if (shown.length === 0) return null;
  return (
    <Section title="Sightings" description={`What was seen of the enemy on turn ${String(turn)}.`}>
      <List size="sm" spacing={4} aria-label="Sightings">
        {shown.map((sighting) => (
          <List.Item key={sighting.id}>
            {manager && (
              <Text span size="sm" fw={600}>
                {armies.find((a) => a.id === sighting.observingArmyId)?.name ?? "An army"} saw{" "}
              </Text>
            )}
            {describeSighting(sighting, armies)}
          </List.Item>
        ))}
      </List>
    </Section>
  );
}
