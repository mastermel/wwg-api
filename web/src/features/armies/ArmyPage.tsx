import { Anchor, Button, Group, Select, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconArrowLeft, IconEdit, IconTrash, IconUserMinus } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import {
  getGetArmyQueryKey,
  getListArmiesQueryKey,
  useAssignCommander,
  useDeleteArmy,
  useGetArmy,
  useRenameArmy,
  useUnassignCommander,
} from "@/api/generated/endpoints/armies/armies";
import {
  getListCampaignMembersQueryKey,
  useGetCampaign,
  useListCampaignMembers,
} from "@/api/generated/endpoints/campaigns/campaigns";
import type { ArmyResponse, CampaignResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { commanderOptions } from "@/features/armies/army-access";
import { ArmyFormModal } from "@/features/armies/ArmyFormModal";
import { useSession } from "@/features/auth/session-context";
import { UnitsSection } from "@/features/units/UnitsSection";
import { canManage } from "@/features/campaigns/campaign-access";
import { ApiError } from "@/lib/api-fetch";
import { useOnline } from "@/lib/use-online";

export function ArmyPage({ campaignId, armyId }: { campaignId: string; armyId: string }) {
  const army = useGetArmy(armyId);
  const campaign = useGetCampaign(campaignId);

  return (
    <Page title={army.data?.name ?? "Army"}>
      <Anchor
        size="sm"
        renderRoot={(props) => <Link to="/campaigns/$id" params={{ id: campaignId }} {...props} />}
      >
        <Group gap={4}>
          <IconArrowLeft size={16} aria-hidden /> {army.data?.campaignName ?? "The campaign"}
        </Group>
      </Anchor>
      <QueryState query={army}>
        {(details) => <ArmyDetails army={details} campaign={campaign.data} />}
      </QueryState>
    </Page>
  );
}

function ArmyDetails({
  army,
  campaign,
}: {
  army: ArmyResponse;
  campaign: CampaignResponse | undefined;
}) {
  const { user } = useSession();
  const manager = campaign !== undefined && canManage(campaign, user);

  return (
    <Stack gap="lg" maw={720}>
      <div>
        <Text size="sm" c="dimmed">
          Commander
        </Text>
        <Text>
          {army.commander ? `${army.commander.firstName} ${army.commander.lastName}` : "Unassigned"}
          {army.commander?.userId === user?.id && (
            <Text span c="dimmed">
              {" "}
              (you)
            </Text>
          )}
        </Text>
      </div>
      {manager && <CommanderControl army={army} />}
      <UnitsSection army={army} manager={manager} />
      {manager && <ArmyActions army={army} />}
    </Stack>
  );
}

/** Choose, change or remove the army's commander (Umpire or Admin). */
function CommanderControl({ army }: { army: ArmyResponse }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const members = useListCampaignMembers(army.campaignId);
  const assign = useAssignCommander();
  const unassign = useUnassignCommander();

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getGetArmyQueryKey(army.id) }),
      queryClient.invalidateQueries({ queryKey: getListArmiesQueryKey(army.campaignId) }),
      queryClient.invalidateQueries({ queryKey: getListCampaignMembersQueryKey(army.campaignId) }),
    ]);

  const choose = async (memberId: string | null) => {
    if (!memberId || memberId === army.commander?.memberId) return;
    try {
      const updated = await assign.mutateAsync({ id: army.id, data: { memberId } });
      queryClient.setQueryData(getGetArmyQueryKey(army.id), updated);
      const name = updated.commander
        ? `${updated.commander.firstName} ${updated.commander.lastName}`
        : "They";
      notifications.show({ color: "green", message: `${name} now commands ${army.name}.` });
      await refresh();
    } catch (error) {
      notifications.show({
        color: "red",
        message:
          error instanceof ApiError && error.problem?.detail
            ? error.problem.detail
            : "The commander couldn't be changed. Try again.",
      });
    }
  };

  const remove = async () => {
    try {
      await unassign.mutateAsync({ id: army.id });
      notifications.show({ color: "green", message: `${army.name} has no commander now.` });
      await refresh();
    } catch {
      notifications.show({
        color: "red",
        message: "The commander couldn't be removed. Try again.",
      });
    }
  };

  const options = commanderOptions(members.data ?? [], army.id);
  return (
    <Group align="flex-end">
      <Select
        label={army.commander ? "Change commander" : "Choose a commander"}
        placeholder={options.length ? "Choose a Player" : "No Players are free"}
        data={options}
        value={army.commander?.memberId ?? null}
        onChange={(value) => void choose(value)}
        disabled={!online || assign.isPending || options.length === 0}
        allowDeselect={false}
        w={{ base: "100%", xs: 280 }}
      />
      {army.commander && (
        <Button
          variant="default"
          leftSection={<IconUserMinus size={16} aria-hidden />}
          onClick={() => void remove()}
          loading={unassign.isPending}
          disabled={!online}
        >
          Remove commander
        </Button>
      )}
    </Group>
  );
}

/** Rename or delete the army (Umpire or Admin). */
function ArmyActions({ army }: { army: ArmyResponse }) {
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const rename = useRenameArmy();
  const remove = useDeleteArmy();
  const [renaming, renameModal] = useDisclosure(false);
  const [deleting, deleteModal] = useDisclosure(false);

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync({ id: army.id });
    } catch {
      notifications.show({ color: "red", message: "The army couldn't be deleted. Try again." });
      deleteModal.close();
      return;
    }
    notifications.show({ color: "green", message: `Deleted ${army.name}.` });
    await navigate({ to: "/campaigns/$id", params: { id: army.campaignId } });
    queryClient.removeQueries({ queryKey: getGetArmyQueryKey(army.id) });
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: getListArmiesQueryKey(army.campaignId) }),
      queryClient.invalidateQueries({ queryKey: getListCampaignMembersQueryKey(army.campaignId) }),
    ]);
  };

  return (
    <Group>
      <Button
        variant="default"
        leftSection={<IconEdit size={16} aria-hidden />}
        onClick={renameModal.open}
        disabled={!online}
      >
        Rename army
      </Button>
      <Button
        color="red"
        variant="light"
        leftSection={<IconTrash size={16} aria-hidden />}
        onClick={deleteModal.open}
        disabled={!online}
      >
        Delete army
      </Button>
      {renaming && (
        <ArmyFormModal
          title="Rename army"
          submitLabel="Save"
          defaultName={army.name}
          onClose={renameModal.close}
          onSubmit={async ({ name }) => {
            const updated = await rename.mutateAsync({ id: army.id, data: { name } });
            queryClient.setQueryData(getGetArmyQueryKey(army.id), updated);
            await queryClient.invalidateQueries({
              queryKey: getListArmiesQueryKey(army.campaignId),
            });
          }}
        />
      )}
      <ConfirmModal
        opened={deleting}
        onClose={deleteModal.close}
        title="Delete this army?"
        confirmLabel="Delete army"
        onConfirm={() => void confirmDelete()}
        loading={remove.isPending}
      >
        {army.name} and its units will be deleted. This can&apos;t be undone.
      </ConfirmModal>
    </Group>
  );
}
