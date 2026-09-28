import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import {
  getListMyCampaignsQueryKey,
  useCreateCampaign,
} from "@/api/generated/endpoints/campaigns/campaigns";
import { BackLink } from "@/components/BackLink";
import { Page } from "@/components/Page";
import { Section } from "@/components/Section";
import { CampaignForm } from "@/features/campaigns/CampaignForm";

export function NewCampaignPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const create = useCreateCampaign();

  return (
    <Page
      title="New campaign"
      summary="You'll be its Umpire: you add the armies and invite the Players."
      back={
        <BackLink renderLink={(props) => <Link to="/campaigns" {...props} />}>Campaigns</BackLink>
      }
    >
      <Section title="Details" description="Players see these on the campaign's page.">
        <CampaignForm
          submitLabel="Create campaign"
          onSubmit={async (values) => {
            const campaign = await create.mutateAsync({ data: values });
            await queryClient.invalidateQueries({ queryKey: getListMyCampaignsQueryKey() });
            await navigate({ to: "/campaigns/$id", params: { id: campaign.id } });
          }}
          onCancel={() => void navigate({ to: "/campaigns" })}
        />
      </Section>
    </Page>
  );
}
