import { Button } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconDoorExit } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import {
  getGetCampaignQueryKey,
  getListCampaignMembersQueryKey,
  getListMyCampaignsQueryKey,
  useLeaveCampaign,
} from "@/api/generated/endpoints/campaigns/campaigns";
import type { CampaignResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { useOnline } from "@/lib/use-online";

/** For Players: leave the campaign. (The Umpire can't; an Admin sets a new one instead.) */
export function LeaveCampaignButton({ campaign }: { campaign: CampaignResponse }) {
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const leave = useLeaveCampaign();
  const [confirming, { open, close }] = useDisclosure(false);

  const confirmLeave = async () => {
    try {
      await leave.mutateAsync({ id: campaign.id });
    } catch {
      notifications.show({ color: "red", message: "You couldn't leave the campaign. Try again." });
      close();
      return;
    }
    notifications.show({ color: "green", message: `You left ${campaign.name}.` });
    await navigate({ to: "/campaigns" });
    // Gone for good: drop the saved copies rather than keep showing them offline.
    queryClient.removeQueries({ queryKey: getGetCampaignQueryKey(campaign.id) });
    queryClient.removeQueries({ queryKey: getListCampaignMembersQueryKey(campaign.id) });
    await queryClient.invalidateQueries({ queryKey: getListMyCampaignsQueryKey() });
  };

  return (
    <>
      <Button
        color="red"
        variant="light"
        leftSection={<IconDoorExit size={16} aria-hidden />}
        onClick={open}
        disabled={!online}
      >
        Leave campaign
      </Button>
      <ConfirmModal
        opened={confirming}
        onClose={close}
        title="Leave this campaign?"
        confirmLabel="Leave campaign"
        onConfirm={() => void confirmLeave()}
        loading={leave.isPending}
      >
        You&apos;ll lose access to {campaign.name}. You can join again with its join link.
      </ConfirmModal>
    </>
  );
}
