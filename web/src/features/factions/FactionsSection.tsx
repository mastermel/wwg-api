import { ActionIcon, Button, Group, Table, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconEdit, IconPlus, IconTrash, IconUsersGroup } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import {
  useCreateFaction,
  useDeleteFaction,
  useListFactions,
  useRenameFaction,
} from "@/api/generated/endpoints/factions/factions";
import type { CampaignResponse, FactionResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { EmptyState } from "@/components/EmptyState";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { FactionFormModal } from "@/features/factions/FactionFormModal";
import { errorMessage } from "@/lib/errors";
import { useConfirmTarget } from "@/lib/use-confirm-target";
import { useOnline } from "@/lib/use-online";

const armies = (count: number) => (count === 1 ? "1 army" : `${String(count)} armies`);

/**
 * The campaign's sides. Every member sees them; the Umpire (or an Admin) adds, renames and
 * deletes them, and puts each army in one (on the army's page).
 */
export function FactionsSection({ campaign }: { campaign: CampaignResponse }) {
  const factions = useListFactions(campaign.id);
  const { user } = useSession();
  const online = useOnline();
  const queryClient = useQueryClient();
  const create = useCreateFaction();
  const rename = useRenameFaction();
  const remove = useDeleteFaction();
  const [creating, createModal] = useDisclosure(false);
  const renaming = useConfirmTarget<FactionResponse>();
  const deleting = useConfirmTarget<FactionResponse>();
  const manager = canManage(campaign, user);
  const refresh = () => refreshCampaign(queryClient, campaign.id);

  const confirmDelete = async (faction: FactionResponse) => {
    try {
      await remove.mutateAsync({ id: faction.id });
      notifications.show({ color: "green", message: `Deleted ${faction.name}.` });
      await refresh();
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The faction couldn't be deleted. Try again."),
      });
    }
    deleting.close();
  };

  return (
    <Section
      title="Factions"
      description="The sides. Every army needs one before the campaign starts."
      flush
      actions={
        manager && (
          <Button
            size="xs"
            leftSection={<IconPlus size={14} aria-hidden />}
            onClick={createModal.open}
            disabled={!online}
          >
            New faction
          </Button>
        )
      }
    >
      <QueryState query={factions}>
        {(list) =>
          list.length === 0 ? (
            <EmptyState icon={IconUsersGroup} title="No factions yet">
              {manager
                ? "Add the sides with New faction, then put each army in one."
                : "The Umpire hasn't added any yet."}
            </EmptyState>
          ) : (
            <Table horizontalSpacing="lg">
              <Table.Tbody>
                {list.map((faction) => (
                  <Table.Tr key={faction.id}>
                    <Table.Td>
                      <Text fw={500} inherit>
                        {faction.name}
                      </Text>
                      <Text size="xs" c="dimmed">
                        {armies(faction.armyCount)}
                      </Text>
                    </Table.Td>
                    {manager && (
                      <Table.Td ta="right">
                        <Group gap={4} justify="flex-end" wrap="nowrap">
                          <ActionIcon
                            variant="subtle"
                            aria-label={`Rename ${faction.name}`}
                            disabled={!online}
                            onClick={() => {
                              renaming.open(faction);
                            }}
                          >
                            <IconEdit size={16} aria-hidden />
                          </ActionIcon>
                          <ActionIcon
                            variant="subtle"
                            color="red"
                            aria-label={`Delete ${faction.name}`}
                            disabled={!online}
                            onClick={() => {
                              deleting.open(faction);
                            }}
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
          )
        }
      </QueryState>
      {creating && (
        <FactionFormModal
          title="New faction"
          submitLabel="Add faction"
          onClose={createModal.close}
          onSubmit={async (values) => {
            const faction = await create.mutateAsync({ id: campaign.id, data: values });
            notifications.show({ color: "green", message: `Added ${faction.name}.` });
            await refresh();
          }}
        />
      )}
      {renaming.opened && renaming.target && (
        <FactionFormModal
          title="Rename faction"
          submitLabel="Save"
          defaultName={renaming.target.name}
          onClose={renaming.close}
          onSubmit={async (values) => {
            const target = renaming.target;
            if (!target) return;
            const faction = await rename.mutateAsync({ id: target.id, data: values });
            notifications.show({ color: "green", message: `Renamed to ${faction.name}.` });
            await refresh();
          }}
        />
      )}
      <ConfirmModal
        opened={deleting.opened}
        onClose={deleting.close}
        title="Delete this faction?"
        confirmLabel="Delete faction"
        onConfirm={() => {
          if (deleting.target) void confirmDelete(deleting.target);
        }}
        loading={remove.isPending}
      >
        {deleting.target &&
          (deleting.target.armyCount > 0
            ? `${deleting.target.name} will be deleted, and its ${armies(deleting.target.armyCount)} left Unassigned.`
            : `${deleting.target.name} will be deleted.`)}
      </ConfirmModal>
    </Section>
  );
}
