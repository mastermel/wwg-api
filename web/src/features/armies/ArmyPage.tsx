import { Button, Grid, Group, Select, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconBooks, IconEdit, IconTrash, IconUser, IconUserMinus } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import {
  getGetArmyQueryKey,
  useAssignCommander,
  useDeleteArmy,
  useGetArmy,
  useUpdateArmy,
  useUnassignCommander,
} from "@/api/generated/endpoints/armies/armies";
import {
  useGetCampaign,
  useListCampaignMembers,
} from "@/api/generated/endpoints/campaigns/campaigns";
import { useListSides } from "@/api/generated/endpoints/sides/sides";
import type { ArmyResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { BackLink } from "@/components/BackLink";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { commanderOptions } from "@/features/armies/army-access";
import { ArmyFormModal } from "@/features/armies/ArmyFormModal";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import { sideOptions } from "@/features/sides/side-options";
import { useSession } from "@/features/auth/session-context";
import { UnitsSection } from "@/features/units/UnitsSection";
import { canManage } from "@/features/campaigns/campaign-access";
import { useOnline } from "@/lib/use-online";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { errorMessage } from "@/lib/errors";

export function ArmyPage({ campaignId, armyId }: { campaignId: string; armyId: string }) {
  const army = useGetArmy(armyId);
  const campaign = useGetCampaign(campaignId);
  const { user } = useSession();
  const manager = campaign.data !== undefined && canManage(campaign.data, user);
  const details = army.data;

  return (
    <Page
      title={details?.name ?? "Army"}
      back={
        <BackLink
          renderLink={(props) => (
            <Link to="/campaigns/$id" params={{ id: campaignId }} {...props} />
          )}
        >
          {details?.campaignName ?? "The campaign"}
        </BackLink>
      }
      summary={details && <ArmySummaryLine army={details} />}
      actions={details && manager && <EditArmyButton army={details} />}
    >
      <QueryState query={army}>
        {(loaded) => <ArmyDetails army={loaded} manager={manager} />}
      </QueryState>
    </Page>
  );
}

/** The army's flag and side, the factions it takes its units from, and who commands it. */
function ArmySummaryLine({ army }: { army: ArmyResponse }) {
  const { user } = useSession();
  return (
    <Group gap="lg" wrap="wrap">
      <ArmyBadge army={army}>{army.side.name}</ArmyBadge>
      <Group gap={6} wrap="nowrap">
        <IconBooks size={16} aria-hidden />
        <Text span inherit>
          {army.factions.length
            ? `Units from ${army.factions.map((faction) => faction.name).join(", ")}`
            : "No factions yet"}
        </Text>
      </Group>
      <Group gap={6} wrap="nowrap">
        <IconUser size={16} aria-hidden />
        <Text span inherit>
          {army.commander
            ? `Commanded by ${army.commander.firstName} ${army.commander.lastName}`
            : "No commander yet"}
          {army.commander?.userId === user?.id && " (you)"}
        </Text>
      </Group>
    </Group>
  );
}

/** Units in the main column; the Umpire's commander choice and danger zone beside them. */
function ArmyDetails({ army, manager }: { army: ArmyResponse; manager: boolean }) {
  if (!manager) {
    return <UnitsSection army={army} manager={false} />;
  }

  return (
    <Grid gap="xl">
      <Grid.Col span={{ base: 12, md: 8 }}>
        <UnitsSection army={army} manager />
      </Grid.Col>
      <Grid.Col span={{ base: 12, md: 4 }}>
        <Stack gap="xl">
          <Section title="Commander" description="A Player who commands no other army.">
            <CommanderControl army={army} />
          </Section>
          <Section title="Danger zone" tone="danger" description="Deletes the army and its units.">
            <DeleteArmyButton army={army} />
          </Section>
        </Stack>
      </Grid.Col>
    </Grid>
  );
}

/** Choose, change or remove the army's commander (Umpire or Admin). */
function CommanderControl({ army }: { army: ArmyResponse }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const members = useListCampaignMembers(army.campaignId);
  const assign = useAssignCommander();
  const unassign = useUnassignCommander();

  const refresh = () => refreshCampaign(queryClient, army.campaignId);

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
        message: errorMessage(error, "The commander couldn't be changed. Try again."),
      });
    }
  };

  const remove = async () => {
    try {
      await unassign.mutateAsync({ id: army.id });
      notifications.show({ color: "green", message: `${army.name} has no commander now.` });
      await refresh();
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The commander couldn't be removed. Try again."),
      });
    }
  };

  const options = commanderOptions(members.data ?? [], army.id);
  return (
    <Stack gap="sm" align="flex-start">
      <Select
        label={army.commander ? "Change commander" : "Choose a commander"}
        placeholder={options.length ? "Choose a Player" : "No Players are free"}
        data={options}
        value={army.commander?.memberId ?? null}
        onChange={(value) => void choose(value)}
        disabled={!online || assign.isPending || options.length === 0}
        allowDeselect={false}
        w="100%"
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
    </Stack>
  );
}

/** Edit the army's name, side, colour, nation and factions (Umpire or Admin): the page's action. */
function EditArmyButton({ army }: { army: ArmyResponse }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const sides = useListSides(army.campaignId);
  const update = useUpdateArmy();
  const [editing, editModal] = useDisclosure(false);

  return (
    <>
      <Button
        variant="default"
        leftSection={<IconEdit size={16} aria-hidden />}
        onClick={editModal.open}
        disabled={!online}
      >
        Edit army
      </Button>
      {editing && (
        <ArmyFormModal
          title="Edit army"
          submitLabel="Save"
          defaultValues={{
            name: army.name,
            commanderMemberId: null,
            sideId: army.side.id,
            color: army.color,
            nation: army.nation,
            factionIds: army.factions.map((faction) => faction.id),
          }}
          sides={sideOptions(sides.data)}
          onClose={editModal.close}
          onSubmit={async ({ name, sideId, color, nation, factionIds }) => {
            const updated = await update.mutateAsync({
              id: army.id,
              data: { name, sideId, color, nation, factionIds },
            });
            queryClient.setQueryData(getGetArmyQueryKey(army.id), updated);
            notifications.show({ color: "green", message: `Saved ${updated.name}.` });
            await refreshCampaign(queryClient, army.campaignId);
          }}
        />
      )}
    </>
  );
}

/** Delete the army and its units (Umpire or Admin), after confirming. */
function DeleteArmyButton({ army }: { army: ArmyResponse }) {
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const remove = useDeleteArmy();
  const [deleting, deleteModal] = useDisclosure(false);

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync({ id: army.id });
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The army couldn't be deleted. Try again."),
      });
      deleteModal.close();
      return;
    }
    notifications.show({ color: "green", message: `Deleted ${army.name}.` });
    await navigate({ to: "/campaigns/$id", params: { id: army.campaignId } });
    queryClient.removeQueries({ queryKey: getGetArmyQueryKey(army.id) });
    await refreshCampaign(queryClient, army.campaignId);
  };

  return (
    <>
      <Button
        color="red"
        variant="light"
        leftSection={<IconTrash size={16} aria-hidden />}
        onClick={deleteModal.open}
        disabled={!online}
      >
        Delete army
      </Button>
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
    </>
  );
}
