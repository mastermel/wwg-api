import { ActionIcon, Button, Group, List, Stack, Text } from "@mantine/core";
import { IconArrowsMove, IconBuildingWarehouse, IconPencil, IconTrash } from "@tabler/icons-react";
import type { ArmySummary, DepotResponse } from "@/api/generated/model";
import { Section } from "@/components/Section";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import { hexName } from "@/features/maps/hex-grid";
import { depotName } from "@/features/maps/use-depots";
import { useOnline } from "@/lib/use-online";

interface DepotsPanelProps {
  depots: readonly DepotResponse[];
  armies: readonly ArmySummary[];
  /** The Umpire (or an Admin), who places, moves, changes and removes them. */
  manager: boolean;
  busy: boolean;
  onAdd: () => void;
  onMove: (depot: DepotResponse) => void;
  onEdit: (depot: DepotResponse) => void;
  onRemove: (depot: DepotResponse) => void;
}

const kindLabel = (depot: DepotResponse) =>
  depot.kind === "Main" ? "Main depot" : "Intermediate depot";

/**
 * The depots the viewer may see, by army (step 48a, decision 0019): the Umpire's, every army's,
 * to place, move, change and remove; a commander's, their own army's.
 */
export function DepotsPanel({
  depots,
  armies,
  manager,
  busy,
  onAdd,
  onMove,
  onEdit,
  onRemove,
}: DepotsPanelProps) {
  const online = useOnline();
  const withDepots = armies.filter((army) => depots.some((d) => d.armyId === army.id));
  if (!manager && withDepots.length === 0) return null;

  return (
    <Section title="Depots" description="Each army's supply comes from its own depots.">
      <Stack gap="md">
        {withDepots.length === 0 && (
          <Text size="sm" c="dimmed">
            No depots yet. Until an army has one, its supply isn&apos;t tracked.
          </Text>
        )}
        {withDepots.map((army) => (
          <Stack key={army.id} gap={4}>
            <Text size="sm" fw={600}>
              <ArmyBadge army={army} />
            </Text>
            <List size="sm" spacing={4} listStyleType="none" aria-label={`${army.name}'s depots`}>
              {depots
                .filter((d) => d.armyId === army.id)
                .map((depot) => (
                  <List.Item key={depot.id}>
                    <Group justify="space-between" wrap="nowrap" gap="xs">
                      <Text size="sm">
                        {depot.name ?? kindLabel(depot)}
                        <Text span size="xs" c="dimmed">
                          {" "}
                          · {depot.name ? `${kindLabel(depot)}, ` : ""}
                          {hexName(depot)}
                        </Text>
                      </Text>
                      {manager && (
                        <Group gap={4} wrap="nowrap">
                          <ActionIcon
                            variant="subtle"
                            aria-label={`Move ${depotName(depot)}`}
                            disabled={!online || busy}
                            onClick={() => {
                              onMove(depot);
                            }}
                          >
                            <IconArrowsMove size={16} aria-hidden />
                          </ActionIcon>
                          <ActionIcon
                            variant="subtle"
                            aria-label={`Edit ${depotName(depot)}`}
                            disabled={!online || busy}
                            onClick={() => {
                              onEdit(depot);
                            }}
                          >
                            <IconPencil size={16} aria-hidden />
                          </ActionIcon>
                          <ActionIcon
                            variant="subtle"
                            color="red"
                            aria-label={`Remove ${depotName(depot)}`}
                            disabled={!online || busy}
                            onClick={() => {
                              onRemove(depot);
                            }}
                          >
                            <IconTrash size={16} aria-hidden />
                          </ActionIcon>
                        </Group>
                      )}
                    </Group>
                  </List.Item>
                ))}
            </List>
          </Stack>
        ))}
        {manager && (
          <Button
            variant="default"
            leftSection={<IconBuildingWarehouse size={16} aria-hidden />}
            disabled={!online || armies.length === 0}
            onClick={onAdd}
          >
            Add depot
          </Button>
        )}
      </Stack>
    </Section>
  );
}
