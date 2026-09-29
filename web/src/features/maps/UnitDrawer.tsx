import { Drawer, Group, NavLink, Stack, Table, Text } from "@mantine/core";
import { useMediaQuery } from "@mantine/hooks";
import type { ReactNode } from "react";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import { armyColorVar } from "@/features/armies/identity/army-colors";
import { describeUnit, type PlacedUnit } from "@/features/maps/stacks";
import { UnitSymbol } from "@/features/units/UnitSymbol";
import { unitTypeLabels } from "@/features/units/unit-types";

interface UnitDrawerProps {
  /** The units chosen on the map: one, or a stack to choose from. Closed when empty. */
  units: readonly PlacedUnit[];
  /** The one being shown, from a stack. */
  selected: PlacedUnit | null;
  onSelect: (unit: PlacedUnit) => void;
  onClose: () => void;
  /** What the viewer can do with the unit (placing it, its orders). */
  actions?: (unit: PlacedUnit) => ReactNode;
}

/**
 * A unit's details, opened by choosing it on the map (a stack lists its units first). From the
 * side on wide screens, from the bottom on phones (DESIGN.md §3.13).
 */
export function UnitDrawer({ units, selected, onSelect, onClose, actions }: UnitDrawerProps) {
  const phone = useMediaQuery("(max-width: 48em)");
  const shown = selected ?? (units.length === 1 ? units[0] : undefined);

  return (
    <Drawer
      opened={units.length > 0}
      onClose={onClose}
      position={phone ? "bottom" : "right"}
      size={phone ? "auto" : "sm"}
      title={shown ? shown.unit.name : `${String(units.length)} units here`}
      closeButtonProps={{ "aria-label": "Close" }}
    >
      {shown ? (
        <Stack>
          <Group gap="sm" wrap="nowrap">
            <UnitSymbol type={shown.unit.type} color={armyColorVar(shown.army.color)} width={40} />
            <Text fw={500}>{unitTypeLabels[shown.unit.type]}</Text>
          </Group>
          <Table variant="vertical" layout="fixed" withTableBorder>
            <Table.Tbody>
              <Table.Tr>
                <Table.Th w={100}>Army</Table.Th>
                <Table.Td>
                  <ArmyBadge army={shown.army} />
                </Table.Td>
              </Table.Tr>
              <Table.Tr>
                <Table.Th>FF</Table.Th>
                <Table.Td>{shown.unit.fightingFactor}</Table.Td>
              </Table.Tr>
              <Table.Tr>
                <Table.Th>Points</Table.Th>
                <Table.Td>{shown.unit.points}</Table.Td>
              </Table.Tr>
            </Table.Tbody>
          </Table>
          {actions?.(shown)}
        </Stack>
      ) : (
        <Stack component="ul" gap={0} p={0} m={0} aria-label="Units here">
          {units.map((placed) => (
            <li key={placed.unit.id} style={{ listStyle: "none" }}>
              <NavLink
                component="button"
                label={placed.unit.name}
                description={`${unitTypeLabels[placed.unit.type]} · ${placed.army.name}`}
                aria-label={describeUnit(placed)}
                leftSection={
                  <UnitSymbol
                    type={placed.unit.type}
                    color={armyColorVar(placed.army.color)}
                    width={30}
                  />
                }
                onClick={() => {
                  onSelect(placed);
                }}
              />
            </li>
          ))}
        </Stack>
      )}
    </Drawer>
  );
}
