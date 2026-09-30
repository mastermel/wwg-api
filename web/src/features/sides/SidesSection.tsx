import { ActionIcon, Button, Group, Table, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconEdit, IconPlus, IconTrash, IconUsersGroup } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import {
  useCreateSide,
  useDeleteSide,
  useListSides,
  useRenameSide,
} from "@/api/generated/endpoints/sides/sides";
import type { CampaignResponse, SideResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { EmptyState } from "@/components/EmptyState";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { SideFormModal } from "@/features/sides/SideFormModal";
import { errorMessage } from "@/lib/errors";
import { useConfirmTarget } from "@/lib/use-confirm-target";
import { useOnline } from "@/lib/use-online";

const armies = (count: number) => (count === 1 ? "1 army" : `${String(count)} armies`);

/**
 * The campaign's sides. Every member sees them; the Umpire (or an Admin) adds, renames and
 * deletes them, and puts each army on one (on the army's page).
 */
export function SidesSection({ campaign }: { campaign: CampaignResponse }) {
  const sides = useListSides(campaign.id);
  const { user } = useSession();
  const online = useOnline();
  const queryClient = useQueryClient();
  const create = useCreateSide();
  const rename = useRenameSide();
  const remove = useDeleteSide();
  const [creating, createModal] = useDisclosure(false);
  const renaming = useConfirmTarget<SideResponse>();
  const deleting = useConfirmTarget<SideResponse>();
  const manager = canManage(campaign, user);
  const refresh = () => refreshCampaign(queryClient, campaign.id);

  const confirmDelete = async (side: SideResponse) => {
    try {
      await remove.mutateAsync({ id: side.id });
      notifications.show({ color: "green", message: `Deleted ${side.name}.` });
      await refresh();
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The side couldn't be deleted. Try again."),
      });
    }
    deleting.close();
  };

  return (
    <Section
      title="Sides"
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
            New side
          </Button>
        )
      }
    >
      <QueryState query={sides}>
        {(list) =>
          list.length === 0 ? (
            <EmptyState icon={IconUsersGroup} title="No sides yet">
              {manager
                ? "Add the sides with New side, then put each army on one."
                : "The Umpire hasn't added any yet."}
            </EmptyState>
          ) : (
            <Table horizontalSpacing="lg">
              <Table.Tbody>
                {list.map((side) => (
                  <Table.Tr key={side.id}>
                    <Table.Td>
                      <Text fw={500} inherit>
                        {side.name}
                      </Text>
                      <Text size="xs" c="dimmed">
                        {armies(side.armyCount)}
                      </Text>
                    </Table.Td>
                    {manager && (
                      <Table.Td ta="right">
                        <Group gap={4} justify="flex-end" wrap="nowrap">
                          <ActionIcon
                            variant="subtle"
                            aria-label={`Rename ${side.name}`}
                            disabled={!online}
                            onClick={() => {
                              renaming.open(side);
                            }}
                          >
                            <IconEdit size={16} aria-hidden />
                          </ActionIcon>
                          <ActionIcon
                            variant="subtle"
                            color="red"
                            aria-label={`Delete ${side.name}`}
                            disabled={!online}
                            onClick={() => {
                              deleting.open(side);
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
        <SideFormModal
          title="New side"
          submitLabel="Add side"
          onClose={createModal.close}
          onSubmit={async (values) => {
            const side = await create.mutateAsync({ id: campaign.id, data: values });
            notifications.show({ color: "green", message: `Added ${side.name}.` });
            await refresh();
          }}
        />
      )}
      {renaming.opened && renaming.target && (
        <SideFormModal
          title="Rename side"
          submitLabel="Save"
          defaultName={renaming.target.name}
          onClose={renaming.close}
          onSubmit={async (values) => {
            const target = renaming.target;
            if (!target) return;
            const side = await rename.mutateAsync({ id: target.id, data: values });
            notifications.show({ color: "green", message: `Renamed to ${side.name}.` });
            await refresh();
          }}
        />
      )}
      <ConfirmModal
        opened={deleting.opened}
        onClose={deleting.close}
        title="Delete this side?"
        confirmLabel="Delete side"
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
