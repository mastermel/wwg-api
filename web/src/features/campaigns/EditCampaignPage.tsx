import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import {
  getGetCampaignQueryKey,
  getListMyCampaignsQueryKey,
  useGetCampaign,
  useUpdateCampaign,
} from "@/api/generated/endpoints/campaigns/campaigns";
import { BackLink } from "@/components/BackLink";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { CampaignForm } from "@/features/campaigns/CampaignForm";

export function EditCampaignPage({ id }: { id: string }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const campaign = useGetCampaign(id);
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
        {(details) => (
          <Section title="Details" description="Players see these on the campaign's page.">
            <CampaignForm
              defaultValues={{ name: details.name, description: details.description ?? "" }}
              submitLabel="Save changes"
              onSubmit={async (values) => {
                const updated = await update.mutateAsync({ id, data: values });
                queryClient.setQueryData(getGetCampaignQueryKey(id), updated);
                await queryClient.invalidateQueries({ queryKey: getListMyCampaignsQueryKey() });
                await back();
              }}
              onCancel={() => void back()}
            />
          </Section>
        )}
      </QueryState>
    </Page>
  );
}
