import { notifications } from "@mantine/notifications";
import { Alert } from "@mantine/core";
import { IconLock } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import {
  getGetCampaignQueryKey,
  useGetCampaign,
  useUpdateCampaign,
} from "@/api/generated/endpoints/campaigns/campaigns";
import { BackLink } from "@/components/BackLink";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { CampaignForm } from "@/features/campaigns/CampaignForm";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";

export function EditCampaignPage({ id }: { id: string }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const campaign = useGetCampaign(id);
  const { user } = useSession();
  const update = useUpdateCampaign();
  const back = () => navigate({ to: "/campaigns/$id", params: { id } });

  return (
    <Page
      title="Edit campaign"
      back={
        <BackLink renderLink={(props) => <Link to="/campaigns/$id" params={{ id }} {...props} />}>
          {campaign.data?.name ?? "The campaign"}
        </BackLink>
      }
    >
      <QueryState query={campaign}>
        {(details) =>
          // A Player can reach this page by its URL; say so, rather than let the save fail.
          !canManage(details, user) ? (
            <Alert
              role="status"
              color="gray"
              icon={<IconLock aria-hidden />}
              title="Only the Umpire can edit this campaign"
            >
              Ask{" "}
              {details.umpire
                ? `${details.umpire.firstName} ${details.umpire.lastName}`
                : "an Admin"}{" "}
              if something needs changing.
            </Alert>
          ) : (
            <Section title="Details" description="Players see these on the campaign's page.">
              <CampaignForm
                defaultValues={{ name: details.name, description: details.description ?? "" }}
                submitLabel="Save changes"
                onSubmit={async (values) => {
                  const updated = await update.mutateAsync({ id, data: values });
                  queryClient.setQueryData(getGetCampaignQueryKey(id), updated);
                  notifications.show({ color: "green", message: `Saved ${updated.name}.` });
                  await refreshCampaign(queryClient, id);
                  await back();
                }}
                onCancel={() => void back()}
              />
            </Section>
          )
        }
      </QueryState>
    </Page>
  );
}
