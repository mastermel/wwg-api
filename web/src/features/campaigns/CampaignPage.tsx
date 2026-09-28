import { Badge, Button, Group, Modal, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconEdit, IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import {
  getListMyCampaignsQueryKey,
  useDeleteCampaign,
  useGetCampaign,
} from "@/api/generated/endpoints/campaigns/campaigns";
import type { CampaignResponse } from "@/api/generated/model";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { useOnline } from "@/lib/use-online";

export function CampaignPage({ id }: { id: string }) {
  const campaign = useGetCampaign(id);
  return (
    <Page title={campaign.data?.name ?? "Campaign"}>
      <QueryState query={campaign}>
        {(details) => <CampaignDetails campaign={details} />}
      </QueryState>
    </Page>
  );
}

function CampaignDetails({ campaign }: { campaign: CampaignResponse }) {
  const { user } = useSession();
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const remove = useDeleteCampaign();
  const [confirming, { open, close }] = useDisclosure(false);

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync({ id: campaign.id });
      await queryClient.invalidateQueries({ queryKey: getListMyCampaignsQueryKey() });
      notifications.show({ color: "green", message: `Deleted ${campaign.name}.` });
      await navigate({ to: "/campaigns" });
    } catch {
      notifications.show({ color: "red", message: "The campaign couldn't be deleted. Try again." });
      close();
    }
  };

  return (
    <Stack gap="lg" maw={720}>
      <Group gap="xs">
        {campaign.myRole && <Badge variant="light">You&apos;re the {campaign.myRole}</Badge>}
        <Text size="sm" c="dimmed">
          Umpire:{" "}
          {campaign.umpire
            ? `${campaign.umpire.firstName} ${campaign.umpire.lastName}`
            : "none (an Admin can set one)"}{" "}
          · {campaign.playerCount === 1 ? "1 player" : `${String(campaign.playerCount)} players`}
        </Text>
      </Group>
      {campaign.description ? (
        <Text style={{ whiteSpace: "pre-wrap" }}>{campaign.description}</Text>
      ) : (
        <Text c="dimmed">No description yet.</Text>
      )}
      {canManage(campaign, user) && (
        <Group>
          <Button
            variant="default"
            leftSection={<IconEdit size={16} aria-hidden />}
            disabled={!online}
            renderRoot={(props) => (
              <Link to="/campaigns/$id/edit" params={{ id: campaign.id }} {...props} />
            )}
          >
            Edit
          </Button>
          <Button
            color="red"
            variant="light"
            leftSection={<IconTrash size={16} aria-hidden />}
            onClick={open}
            disabled={!online}
          >
            Delete campaign
          </Button>
        </Group>
      )}
      <Modal opened={confirming} onClose={close} title="Delete this campaign?" centered>
        <Text size="sm">
          {campaign.name} and everything in it will be deleted for everyone. This can&apos;t be
          undone.
        </Text>
        <Group justify="flex-end" mt="lg">
          <Button variant="default" onClick={close}>
            Cancel
          </Button>
          <Button color="red" loading={remove.isPending} onClick={() => void confirmDelete()}>
            Delete campaign
          </Button>
        </Group>
      </Modal>
    </Stack>
  );
}
