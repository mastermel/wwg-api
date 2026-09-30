import { ActionIcon, Button, Group, Stack, Table, Text, VisuallyHidden } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconEdit, IconPlus, IconShield, IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import {
  getGetFactionQueryKey,
  getListFactionsQueryKey,
  useCreateUnit,
  useDeleteFaction,
  useDeleteUnit,
  useGetFaction,
  useUpdateFaction,
  useUpdateUnit,
} from "@/api/generated/endpoints/library/library";
import type { FactionResponse, UnitResponse } from "@/api/generated/model";
import { BackLink } from "@/components/BackLink";
import { ConfirmModal } from "@/components/ConfirmModal";
import { EmptyState } from "@/components/EmptyState";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { NationFlag } from "@/features/armies/identity/NationFlag";
import { nationLabel } from "@/features/armies/identity/nations";
import { useSession } from "@/features/auth/session-context";
import { FactionFormModal } from "@/features/library/FactionFormModal";
import { canEditLibrary, unitCount } from "@/features/library/library-access";
import { UnitFormModal } from "@/features/units/UnitFormModal";
import { unitTypeLabels } from "@/features/units/unit-types";
import { errorMessage } from "@/lib/errors";
import { useConfirmTarget } from "@/lib/use-confirm-target";
import { useOnline } from "@/lib/use-online";

/** A library faction and its units; Managers and Admins edit them here (decision 0015). */
export function FactionPage({ id }: { id: string }) {
  const faction = useGetFaction(id);
  const { user } = useSession();
  const editor = canEditLibrary(user);

  return (
    <Page
      title={faction.data?.name ?? "Faction"}
      back={<BackLink renderLink={(props) => <Link to="/library" {...props} />}>Library</BackLink>}
      summary={
        faction.data && (
          <Group gap="xs">
            <NationFlag nation={faction.data.nation} plainColor="var(--mantine-color-gray-5)" />
            <Text span inherit>
              {faction.data.nation === "None" ? "No nation" : nationLabel(faction.data.nation)} ·{" "}
              {unitCount(faction.data.units.length)}
            </Text>
          </Group>
        )
      }
    >
      <QueryState query={faction}>
        {(loaded) => <FactionView faction={loaded} editor={editor} />}
      </QueryState>
    </Page>
  );
}

function FactionView({ faction, editor }: { faction: FactionResponse; editor: boolean }) {
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const updateFaction = useUpdateFaction();
  const removeFaction = useDeleteFaction();
  const createUnit = useCreateUnit();
  const updateUnit = useUpdateUnit();
  const removeUnit = useDeleteUnit();
  const [editingFaction, factionModal] = useDisclosure(false);
  const [deletingFaction, deleteFactionModal] = useDisclosure(false);
  const [adding, addModal] = useDisclosure(false);
  const [editing, setEditing] = useState<UnitResponse | null>(null);
  const deleting = useConfirmTarget<UnitResponse>();
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getGetFactionQueryKey(faction.id) }),
      queryClient.invalidateQueries({ queryKey: getListFactionsQueryKey() }),
    ]);

  const confirmDeleteUnit = async (unit: UnitResponse) => {
    try {
      await removeUnit.mutateAsync({ id: unit.id });
      notifications.show({ color: "green", message: `Deleted ${unit.name}.` });
      await refresh();
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The unit couldn't be deleted. Try again."),
      });
    }
    deleting.close();
  };

  const confirmDeleteFaction = async () => {
    try {
      await removeFaction.mutateAsync({ id: faction.id });
      notifications.show({ color: "green", message: `Deleted ${faction.name}.` });
      await queryClient.invalidateQueries({ queryKey: getListFactionsQueryKey() });
      await navigate({ to: "/library" });
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The faction couldn't be deleted. Try again."),
      });
      deleteFactionModal.close();
    }
  };

  return (
    <Stack gap="xl">
      <Section
        title="Units"
        description={editor ? "Name, type, Fighting Factor (FF) and points." : undefined}
        flush
        actions={
          editor && (
            <Group gap="xs">
              <Button
                size="xs"
                variant="default"
                leftSection={<IconEdit size={14} aria-hidden />}
                onClick={factionModal.open}
                disabled={!online}
              >
                Edit faction
              </Button>
              <Button
                size="xs"
                leftSection={<IconPlus size={14} aria-hidden />}
                onClick={addModal.open}
                disabled={!online}
              >
                Add unit
              </Button>
            </Group>
          )
        }
      >
        {faction.units.length === 0 ? (
          <EmptyState icon={IconShield} title="No units yet">
            {editor ? "Add one with Add unit." : "A Manager hasn't added any yet."}
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
                {editor && (
                  <Table.Th>
                    <VisuallyHidden>Actions</VisuallyHidden>
                  </Table.Th>
                )}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {faction.units.map((unit) => (
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
                  {editor && (
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
          </Table>
        )}
      </Section>
      {editor && (
        <Section
          title="Delete faction"
          tone="danger"
          description="Only a faction without units can be deleted."
        >
          <Group>
            <Button
              color="red"
              variant="light"
              leftSection={<IconTrash size={16} aria-hidden />}
              onClick={deleteFactionModal.open}
              disabled={!online || faction.units.length > 0}
            >
              Delete faction
            </Button>
          </Group>
        </Section>
      )}
      {editingFaction && (
        <FactionFormModal
          title="Edit faction"
          submitLabel="Save"
          defaultValues={{ name: faction.name, nation: faction.nation }}
          onClose={factionModal.close}
          onSubmit={async (values) => {
            const saved = await updateFaction.mutateAsync({ id: faction.id, data: values });
            notifications.show({ color: "green", message: `Saved ${saved.name}.` });
            await refresh();
          }}
        />
      )}
      {adding && (
        <UnitFormModal
          title="Add unit"
          submitLabel="Add unit"
          onClose={addModal.close}
          onSubmit={async (values) => {
            const unit = await createUnit.mutateAsync({ id: faction.id, data: values });
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
            const unit = await updateUnit.mutateAsync({ id: editing.id, data: values });
            notifications.show({ color: "green", message: `Saved ${unit.name}.` });
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
          if (deleting.target) void confirmDeleteUnit(deleting.target);
        }}
        loading={removeUnit.isPending}
      >
        {deleting.target?.name} will be removed from the library. This can&apos;t be undone.
      </ConfirmModal>
      <ConfirmModal
        opened={deletingFaction}
        onClose={deleteFactionModal.close}
        title="Delete this faction?"
        confirmLabel="Delete faction"
        onConfirm={() => void confirmDeleteFaction()}
        loading={removeFaction.isPending}
      >
        {faction.name} will be deleted from the library. This can&apos;t be undone.
      </ConfirmModal>
    </Stack>
  );
}
