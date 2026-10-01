import { Accordion, Group, List, Stack, Table, Text } from "@mantine/core";
import type { ArmySummary, ScoreboardResponse } from "@/api/generated/model";
import { Section } from "@/components/Section";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import { hexName } from "@/features/maps/hex-grid";
import { victoryPoints } from "@/features/maps/victory";

interface ScoreboardPanelProps {
  scoreboard: ScoreboardResponse | undefined;
  armies: readonly ArmySummary[];
}

const placeName = (s: { name?: string | null; q: number; r: number }) => s.name ?? hexName(s);

/**
 * The campaign's victory points (step 50, decision 0021): each side's total, with its armies'
 * parts; the settlements the viewer's side holds; and the history, each turn's totals and the
 * changes the viewer may know of. Nothing until there are any points to score.
 */
export function ScoreboardPanel({ scoreboard, armies }: ScoreboardPanelProps) {
  if (
    !scoreboard ||
    (scoreboard.sides.every((s) => s.points === 0) && scoreboard.settlements.length === 0)
  ) {
    return null;
  }
  const army = (id?: string | null) => armies.find((a) => a.id === id);
  const armyName = (id?: string | null) => army(id)?.name ?? "no one";

  return (
    <Section title="Victory points" description="Towns, cities and fortresses held, by side.">
      <Stack gap="md">
        <Table layout="fixed" aria-label="Victory points by side">
          <Table.Tbody>
            {scoreboard.sides.map((side) => (
              <Table.Tr key={side.sideId}>
                <Table.Td>
                  <Text size="sm" fw={600}>
                    {side.name}
                  </Text>
                  {side.armies
                    .filter((a) => a.points > 0)
                    .map((a) => (
                      <Text key={a.armyId} size="xs" c="dimmed">
                        {armyName(a.armyId)}: {victoryPoints(a.points)}
                      </Text>
                    ))}
                </Table.Td>
                <Table.Td w={90} ta="right">
                  <Text size="sm" fw={600}>
                    {victoryPoints(side.points)}
                  </Text>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
        {scoreboard.settlements.length > 0 && (
          <Stack gap={4}>
            <Text size="sm" fw={600}>
              Held
            </Text>
            <List size="sm" spacing={2} listStyleType="none" p={0} aria-label="Settlements held">
              {scoreboard.settlements.map((s) => {
                const holder = army(s.armyId);
                return (
                  <List.Item key={`${String(s.q)},${String(s.r)}`}>
                    <Group gap="xs" wrap="nowrap" justify="space-between">
                      <Text size="sm">
                        {placeName(s)} ({victoryPoints(s.value)})
                      </Text>
                      {holder ? (
                        <Text size="sm" span>
                          <ArmyBadge army={holder} />
                        </Text>
                      ) : (
                        <Text size="xs" c="dimmed">
                          No one
                        </Text>
                      )}
                    </Group>
                  </List.Item>
                );
              })}
            </List>
          </Stack>
        )}
        <Accordion variant="contained">
          <Accordion.Item value="history">
            <Accordion.Control>
              <Text size="sm">History</Text>
            </Accordion.Control>
            <Accordion.Panel>
              <Stack gap="sm">
                <Table aria-label="Totals after each turn">
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Turn</Table.Th>
                      {scoreboard.sides.map((side) => (
                        <Table.Th key={side.sideId} ta="right">
                          {side.name}
                        </Table.Th>
                      ))}
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {[...scoreboard.turns].reverse().map((turn) => (
                      <Table.Tr key={turn.turn}>
                        <Table.Td>{turn.turn === 0 ? "Setup" : turn.turn}</Table.Td>
                        {turn.sides.map((side) => (
                          <Table.Td key={side.sideId} ta="right">
                            {side.points}
                          </Table.Td>
                        ))}
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
                {scoreboard.changes.length > 0 && (
                  <List size="sm" spacing={2} aria-label="Changes of hands">
                    {[...scoreboard.changes].reverse().map((c, index) => (
                      <List.Item key={`${String(c.turn)}-${String(index)}`}>
                        {c.turn === 0 ? "Setup" : `Turn ${String(c.turn)}`}: {placeName(c)} (
                        {victoryPoints(c.value)}){" "}
                        {c.byUmpire
                          ? `given to ${armyName(c.toArmyId)} by the Umpire`
                          : `taken by ${armyName(c.toArmyId)}${c.fromArmyId ? ` from ${armyName(c.fromArmyId)}` : ""}`}
                        .
                      </List.Item>
                    ))}
                  </List>
                )}
              </Stack>
            </Accordion.Panel>
          </Accordion.Item>
        </Accordion>
      </Stack>
    </Section>
  );
}
