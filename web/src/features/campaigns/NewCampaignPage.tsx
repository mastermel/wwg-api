import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import {
  getListMyCampaignsQueryKey,
  useCreateCampaign,
} from "@/api/generated/endpoints/campaigns/campaigns";
import { Page } from "@/components/Page";
import { CampaignForm } from "@/features/campaigns/CampaignForm";

export function NewCampaignPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const create = useCreateCampaign();

  return (
    <Page title="New campaign">
      <CampaignForm
        submitLabel="Create campaign"
        onSubmit={async (values) => {
          const campaign = await create.mutateAsync({ data: values });
          await queryClient.invalidateQueries({ queryKey: getListMyCampaignsQueryKey() });
          await navigate({ to: "/campaigns/$id", params: { id: campaign.id } });
        }}
        onCancel={() => void navigate({ to: "/campaigns" })}
      />
    </Page>
  );
}
