import {
  ActionIcon,
  Button,
  Group,
  Stack,
  Table,
  Text,
  Title,
  VisuallyHidden,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconEdit, IconPlus, IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { getGetArmyQueryKey } from "@/api/generated/endpoints/armies/armies";
import { useCreateUnit, useDeleteUnit, useUpdateUnit } from "@/api/generated/endpoints/units/units";
import type { ArmyResponse, UnitResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { UnitFormModal } from "@/features/units/UnitFormModal";
import { unitTypeLabels } from "@/features/units/unit-types";
import { useConfirmTarget } from "@/lib/use-confirm-target";
import { useOnline } from "@/lib/use-online";

/**
 * The army's units. Only its commander, the Umpire and Admins get this far (the army page is
 * theirs); the Umpire (or an Admin) can add, edit and delete them.
 */
export function UnitsSection({ army, manager }: { army: ArmyResponse; manager: boolean }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const create = useCreateUnit();
  const update = useUpdateUnit();
  const remove = useDeleteUnit();
  const [adding, addModal] = useDisclosure(false);
  const [editing, setEditing] = useState<UnitResponse | null>(null);
  const deleting = useConfirmTarget<UnitResponse>();
  const refresh = () => queryClient.invalidateQueries({ queryKey: getGetArmyQueryKey(army.id) });
  const totalPoints = army.units.reduce((sum, unit) => sum + unit.points, 0);

  const confirmDelete = async (unit: UnitResponse) => {
    try {
      await remove.mutateAsync({ id: unit.id });
      notifications.show({ color: "green", message: `Deleted ${unit.name}.` });
      await refresh();
    } catch {
      notifications.show({ color: "red", message: "The unit couldn't be deleted. Try again." });
    }
    deleting.close();
  };

  return (
    <Stack gap="sm" component="section" aria-labelledby="units-heading">
      <Group justify="space-between">
        <Title order={2} size="h3" id="units-heading">
          Units
        </Title>
        {manager && (
          <Button
            size="xs"
            leftSection={<IconPlus size={14} aria-hidden />}
            onClick={addModal.open}
            disabled={!online}
          >
            Add unit
          </Button>
        )}
      </Group>
      {army.units.length === 0 ? (
        <Text c="dimmed">{manager ? "No units yet. Add one with Add unit." : "No units yet."}</Text>
      ) : (
        <Table>
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
                        aria-label={`Delete ${unit.name}`}
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
          <Table.Tfoot>
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
      {adding && (
        <UnitFormModal
          title="Add unit"
          submitLabel="Add unit"
          onClose={addModal.close}
          onSubmit={async (values) => {
            const unit = await create.mutateAsync({ id: army.id, data: values });
            notifications.show({ color: "green", message: `Added ${unit.name}.` });
            await refresh();
          }}
        />
      )}
      {editing && (
        <UnitFormModal
          title="Edit unit"
          submitLabel="Save"
          defaultValues={editing}
          onClose={() => {
            setEditing(null);
          }}
          onSubmit={async (values) => {
            await update.mutateAsync({ id: editing.id, data: values });
            await refresh();
          }}
        />
      )}
      <ConfirmModal
        opened={deleting.opened}
        onClose={deleting.close}
        title="Delete this unit?"
        confirmLabel="Delete unit"
        onConfirm={() => {
          if (deleting.target) void confirmDelete(deleting.target);
        }}
        loading={remove.isPending}
      >
        {deleting.target?.name} will be removed from {army.name}. This can&apos;t be undone.
      </ConfirmModal>
    </Stack>
  );
}
