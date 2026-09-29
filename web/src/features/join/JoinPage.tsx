import { Alert, Anchor, Button, Stack, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { IconLinkOff } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useGetJoinPreview, useJoinCampaign } from "@/api/generated/endpoints/join/join";
import type { JoinPreviewResponse } from "@/api/generated/model";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { useSession } from "@/features/auth/session-context";
import { ApiError } from "@/lib/api-fetch";
import { useOnline } from "@/lib/use-online";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { errorMessage } from "@/lib/errors";

/**
 * Where a join link lands. Anyone can see which campaign it's for; signed out, it offers sign-in
 * or register, which come back here afterwards.
 */
export function JoinPage({ code }: { code: string }) {
  // Not saved for offline use: joining needs the network anyway.
  const preview = useGetJoinPreview(code, { query: { meta: { persist: false } } });

  return (
    <Page title="Join a campaign">
      {preview.error instanceof ApiError && preview.error.status === 404 ? (
        <Alert
          role="status"
          color="gray"
          icon={<IconLinkOff aria-hidden />}
          title="This join link doesn't work"
        >
          It may have been replaced by a new one. Ask your Umpire for the current link.
        </Alert>
      ) : (
        <QueryState query={preview}>
          {(campaign) => <JoinOffer code={code} campaign={campaign} />}
        </QueryState>
      )}
    </Page>
  );
}

function JoinOffer({ code, campaign }: { code: string; campaign: JoinPreviewResponse }) {
  const { status } = useSession();
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const join = useJoinCampaign();
  const here = `/join/${code}`;

  const onJoin = async () => {
    try {
      const joined = await join.mutateAsync({ code });
      notifications.show({
        color: "green",
        message:
          joined.myRole === "Player"
            ? `You're in ${campaign.campaignName}.`
            : `You're already the ${joined.myRole} of ${campaign.campaignName}.`,
      });
      await refreshCampaign(queryClient, joined.campaignId);
      await navigate({ to: "/campaigns/$id", params: { id: joined.campaignId } });
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "You couldn't join. Try again."),
      });
    }
  };

  return (
    <Stack gap="md">
      <Text>
        You&apos;ve been invited to join{" "}
        <Text span fw={700}>
          {campaign.campaignName}
        </Text>
        {campaign.umpireName && `, run by ${campaign.umpireName}`}.
      </Text>
      {status === "signed-out" ? (
        <>
          <Text size="sm" c="dimmed">
            Sign in or create an account to join. You&apos;ll come back here afterwards.
          </Text>
          <Button
            renderRoot={(props) => <Link to="/sign-in" search={{ redirect: here }} {...props} />}
          >
            Sign in to join
          </Button>
          <Button
            variant="default"
            renderRoot={(props) => <Link to="/register" search={{ redirect: here }} {...props} />}
          >
            Create an account
          </Button>
        </>
      ) : (
        <>
          <Button onClick={() => void onJoin()} loading={join.isPending} disabled={!online}>
            Join as a Player
          </Button>
          {!online && (
            <Text size="sm" c="dimmed">
              You&apos;re offline. You can join once you&apos;re back online.
            </Text>
          )}
          <Anchor ta="center" size="sm" renderRoot={(props) => <Link to="/campaigns" {...props} />}>
            Not now
          </Anchor>
        </>
      )}
    </Stack>
  );
}
