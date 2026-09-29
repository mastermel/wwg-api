import { Badge, Button, Grid, Group, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconCrown, IconEdit, IconTrash, IconUsers } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useDeleteCampaign, useGetCampaign } from "@/api/generated/endpoints/campaigns/campaigns";
import type { CampaignResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { ArmiesSection } from "@/features/armies/ArmiesSection";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { JoinLinkSection } from "@/features/campaigns/JoinLinkSection";
import { LeaveCampaignButton } from "@/features/campaigns/LeaveCampaignButton";
import { MembersSection } from "@/features/campaigns/MembersSection";
import { SetUmpireButton } from "@/features/campaigns/SetUmpireButton";
import { useOnline } from "@/lib/use-online";
import { forgetCampaign } from "@/features/campaigns/campaign-cache";
import { errorMessage } from "@/lib/errors";

export function CampaignPage({ id }: { id: string }) {
  const campaign = useGetCampaign(id);
  const { user } = useSession();
  const online = useOnline();
  const details = campaign.data;
  const manager = details !== undefined && canManage(details, user);

  return (
    <Page
      title={details?.name ?? "Campaign"}
      summary={details && <CampaignSummary campaign={details} />}
      actions={
        details &&
        manager && (
          <>
            <Button
              variant="default"
              leftSection={<IconEdit size={16} aria-hidden />}
              disabled={!online}
              renderRoot={(props) => (
                <Link to="/campaigns/$id/edit" params={{ id: details.id }} {...props} />
              )}
            >
              Edit
            </Button>
            {user?.isAdmin && <SetUmpireButton campaign={details} />}
          </>
        )
      }
    >
      <QueryState query={campaign}>
        {(loaded) => <CampaignDetails campaign={loaded} manager={manager} />}
      </QueryState>
    </Page>
  );
}

/** The line under the title: my role, the Umpire and how many Players. */
function CampaignSummary({ campaign }: { campaign: CampaignResponse }) {
  return (
    <Group gap="md" wrap="wrap">
      {campaign.myRole && (
        <Badge variant="light">
          {campaign.myRole === "Umpire" ? "You're the Umpire" : "You're a Player"}
        </Badge>
      )}
      <Group gap={6} wrap="nowrap">
        <IconCrown size={16} aria-hidden />
        <Text span inherit>
          Umpire:{" "}
          {campaign.umpire
            ? `${campaign.umpire.firstName} ${campaign.umpire.lastName}`
            : "none (an Admin can set one)"}
        </Text>
      </Group>
      <Group gap={6} wrap="nowrap">
        <IconUsers size={16} aria-hidden />
        <Text span inherit>
          {campaign.playerCount === 1 ? "1 player" : `${String(campaign.playerCount)} players`}
        </Text>
      </Group>
    </Group>
  );
}

/**
 * Wide screens: the campaign's forces (armies, members) in the main column, and what it is (its
 * description, the join link, the danger zone) beside them. Phones: one column, forces first.
 */
function CampaignDetails({ campaign, manager }: { campaign: CampaignResponse; manager: boolean }) {
  return (
    <Grid gap="xl">
      <Grid.Col span={{ base: 12, md: 8 }}>
        <Stack gap="xl">
          <ArmiesSection campaign={campaign} />
          <MembersSection campaign={campaign} />
        </Stack>
      </Grid.Col>
      <Grid.Col span={{ base: 12, md: 4 }}>
        <Stack gap="xl">
          <Section title="About">
            {campaign.description ? (
              <Text style={{ whiteSpace: "pre-wrap" }}>{campaign.description}</Text>
            ) : (
              <Text c="dimmed">No description yet.</Text>
            )}
          </Section>
          {manager && <JoinLinkSection campaign={campaign} />}
          {manager && (
            <Section
              title="Danger zone"
              tone="danger"
              description="Deletes the campaign, its armies and units, for everyone."
            >
              <DeleteCampaignButton campaign={campaign} />
            </Section>
          )}
          {campaign.myRole === "Player" && (
            <Section
              title="Leave"
              tone="danger"
              description="You'll lose access. You can join again with its join link."
            >
              <LeaveCampaignButton campaign={campaign} />
            </Section>
          )}
        </Stack>
      </Grid.Col>
    </Grid>
  );
}

function DeleteCampaignButton({ campaign }: { campaign: CampaignResponse }) {
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const remove = useDeleteCampaign();
  const [confirming, { open, close }] = useDisclosure(false);

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync({ id: campaign.id });
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The campaign couldn't be deleted. Try again."),
      });
      close();
      return;
    }
    notifications.show({ color: "green", message: `Deleted ${campaign.name}.` });
    await navigate({ to: "/campaigns" });
    // Gone for good: drop the saved copies rather than keep showing them offline.
    await forgetCampaign(queryClient, campaign.id);
  };

  return (
    <>
      <Button
        color="red"
        variant="light"
        leftSection={<IconTrash size={16} aria-hidden />}
        onClick={open}
        disabled={!online}
      >
        Delete campaign
      </Button>
      <ConfirmModal
        opened={confirming}
        onClose={close}
        title="Delete this campaign?"
        confirmLabel="Delete campaign"
        onConfirm={() => void confirmDelete()}
        loading={remove.isPending}
      >
        {campaign.name} and everything in it will be deleted for everyone. This can&apos;t be
        undone.
      </ConfirmModal>
    </>
  );
}
