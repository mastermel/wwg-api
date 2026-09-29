import { Alert, Box } from "@mantine/core";
import { IconCloudOff, IconMap, IconSettings } from "@tabler/icons-react";
import { Link } from "@tanstack/react-router";
import { useGetCampaign } from "@/api/generated/endpoints/campaigns/campaigns";
import { useGetCampaignMap } from "@/api/generated/endpoints/maps/maps";
import { BackLink } from "@/components/BackLink";
import { EmptyState } from "@/components/EmptyState";
import { LinkButton } from "@/components/LinkButton";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { CampaignMap } from "@/features/maps/CampaignMap";
import { useOnline } from "@/lib/use-online";

/** The campaign's map, for every member (DESIGN.md §3.13). Online only. */
export function MapPage({ campaignId }: { campaignId: string }) {
  const campaign = useGetCampaign(campaignId);
  // The map is online only: nothing about it is saved for offline use.
  const map = useGetCampaignMap(campaignId, { query: { meta: { persist: false } } });
  const { user } = useSession();
  const online = useOnline();
  const manager = campaign.data !== undefined && canManage(campaign.data, user);

  const settingsButton = (
    <LinkButton
      variant="default"
      leftSection={<IconSettings size={16} aria-hidden />}
      disabled={!online}
      renderLink={(props) => (
        <Link to="/campaigns/$id/map/settings" params={{ id: campaignId }} {...props} />
      )}
    >
      Map settings
    </LinkButton>
  );

  return (
    <Page
      title="Map"
      actions={manager && settingsButton}
      back={
        <BackLink
          renderLink={(props) => (
            <Link to="/campaigns/$id" params={{ id: campaignId }} {...props} />
          )}
        >
          {campaign.data?.name ?? "The campaign"}
        </BackLink>
      }
    >
      {!online ? (
        <Alert
          role="status"
          color="gray"
          icon={<IconCloudOff aria-hidden />}
          title="The map needs a connection"
        >
          It isn&apos;t saved on this device. It will load when you&apos;re back online.
        </Alert>
      ) : (
        <QueryState query={map}>
          {(settings) =>
            settings.bounds ? (
              // The map takes the rest of the screen, and at least enough to be useful.
              <Box h="calc(100dvh - 15rem)" mih={360}>
                <CampaignMap settings={settings} bounds={settings.bounds} />
              </Box>
            ) : (
              <EmptyState icon={IconMap} title="No map yet" action={manager && settingsButton}>
                {manager
                  ? "Choose the campaign's area in the map settings."
                  : "The Umpire hasn't chosen the campaign's area yet."}
              </EmptyState>
            )
          }
        </QueryState>
      )}
    </Page>
  );
}
