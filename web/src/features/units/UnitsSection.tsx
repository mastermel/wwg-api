import { ActionIcon, Button, Group, Table, Text, VisuallyHidden } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconEdit, IconPlus, IconShield, IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { getGetArmyQueryKey } from "@/api/generated/endpoints/armies/armies";
import {
  useDeleteArmyUnit,
  useUpdateArmyUnit,
} from "@/api/generated/endpoints/army-units/army-units";
import type { ArmyResponse, ArmyUnitResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { EmptyState } from "@/components/EmptyState";
import { Section } from "@/components/Section";
import { AddUnitsModal } from "@/features/units/AddUnitsModal";
import classes from "@/features/units/UnitsSection.module.css";
import { UnitFormModal } from "@/features/units/UnitFormModal";
import { unitTypeLabels } from "@/features/units/unit-types";
import { useConfirmTarget } from "@/lib/use-confirm-target";
import { useOnline } from "@/lib/use-online";
import { errorMessage } from "@/lib/errors";

/**
 * The army's units: the campaign's copies of library units. The Umpire (or an Admin) adds them
 * from the army's factions, and edits and removes the copies.
 */
export function UnitsSection({ army, manager }: { army: ArmyResponse; manager: boolean }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateArmyUnit();
  const remove = useDeleteArmyUnit();
  const [adding, addModal] = useDisclosure(false);
  const [editing, setEditing] = useState<ArmyUnitResponse | null>(null);
  const deleting = useConfirmTarget<ArmyUnitResponse>();
  const refresh = () => queryClient.invalidateQueries({ queryKey: getGetArmyQueryKey(army.id) });
  const totalPoints = army.units.reduce((sum, unit) => sum + unit.points, 0);

  const confirmDelete = async (unit: ArmyUnitResponse) => {
    try {
      await remove.mutateAsync({ id: unit.id });
      notifications.show({ color: "green", message: `Removed ${unit.name}.` });
      await refresh();
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The unit couldn't be removed. Try again."),
      });
    }
    deleting.close();
  };

  return (
    <Section
      title="Units"
      description={
        manager
          ? "From the library. Editing one here changes this campaign's copy only."
          : undefined
      }
      flush
      actions={
        manager && (
          <Button
            size="xs"
            leftSection={<IconPlus size={14} aria-hidden />}
            onClick={addModal.open}
            disabled={!online}
          >
            Add units
          </Button>
        )
      }
    >
      {army.units.length === 0 ? (
        <EmptyState icon={IconShield} title="No units yet">
          {manager
            ? "Choose them from the library with Add units."
            : "The Umpire hasn't added any yet."}
        </EmptyState>
      ) : (
        <Table horizontalSpacing="lg" highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Name</Table.Th>
              {/* Phones show the type under the name instead, rather than scroll sideways. */}
              <Table.Th visibleFrom="sm">Type</Table.Th>
              <Table.Th ta="right">
                <abbr title="Fighting Factor">FF</abbr>
              </Table.Th>
              <Table.Th ta="right">Points</Table.Th>
              {manager && (
                <Table.Th>
                  <VisuallyHidden>Actions</VisuallyHidden>
                </Table.Th>
              )}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {army.units.map((unit) => (
              <Table.Tr key={unit.id}>
                <Table.Td>
                  {unit.name}
                  <Text size="xs" c="dimmed" hiddenFrom="sm">
                    {unitTypeLabels[unit.type]}
                  </Text>
                </Table.Td>
                <Table.Td visibleFrom="sm">{unitTypeLabels[unit.type]}</Table.Td>
                <Table.Td ta="right">{unit.fightingFactor}</Table.Td>
                <Table.Td ta="right">{unit.points}</Table.Td>
                {manager && (
                  <Table.Td>
                    <Group gap={4} justify="flex-end" wrap="nowrap">
                      <ActionIcon
                        variant="subtle"
                        aria-label={`Edit ${unit.name}`}
                        onClick={() => {
                          setEditing(unit);
                        }}
                        disabled={!online}
                      >
                        <IconEdit size={16} aria-hidden />
                      </ActionIcon>
                      <ActionIcon
                        variant="subtle"
                        color="red"
                        aria-label={`Remove ${unit.name}`}
                        onClick={() => {
                          deleting.open(unit);
                        }}
                        disabled={!online}
                      >
                        <IconTrash size={16} aria-hidden />
                      </ActionIcon>
                    </Group>
                  </Table.Td>
                )}
              </Table.Tr>
            ))}
          </Table.Tbody>
          <Table.Tfoot className={classes.totals}>
            <Table.Tr>
              <Table.Th scope="row">
                {army.units.length === 1 ? "1 unit" : `${String(army.units.length)} units`}
              </Table.Th>
              <Table.Td visibleFrom="sm" />
              <Table.Td />
              <Table.Td ta="right" fw={700}>
                {totalPoints}
              </Table.Td>
              {manager && <Table.Td />}
            </Table.Tr>
          </Table.Tfoot>
        </Table>
      )}
      {adding && <AddUnitsModal army={army} onClose={addModal.close} />}
      {editing && (
        <UnitFormModal
          title="Edit unit"
          submitLabel="Save"
          defaultValues={editing}
          onClose={() => {
            setEditing(null);
          }}
          onSubmit={async (values) => {
            const unit = await update.mutateAsync({ id: editing.id, data: values });
            notifications.show({ color: "green", message: `Saved ${unit.name}.` });
            await refresh();
          }}
        />
      )}
      <ConfirmModal
        opened={deleting.opened}
        onClose={deleting.close}
        title="Remove this unit?"
        confirmLabel="Remove unit"
        onConfirm={() => {
          if (deleting.target) void confirmDelete(deleting.target);
        }}
        loading={remove.isPending}
      >
        {deleting.target?.name} will be removed from {army.name}, and any changes made to it here
        lost. The library keeps its own.
      </ConfirmModal>
    </Section>
  );
}
