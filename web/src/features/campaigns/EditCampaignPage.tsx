import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import {
  getGetCampaignQueryKey,
  getListMyCampaignsQueryKey,
  useGetCampaign,
  useUpdateCampaign,
} from "@/api/generated/endpoints/campaigns/campaigns";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { CampaignForm } from "@/features/campaigns/CampaignForm";

export function EditCampaignPage({ id }: { id: string }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const campaign = useGetCampaign(id);
  const update = useUpdateCampaign();
  const back = () => navigate({ to: "/campaigns/$id", params: { id } });

  return (
    <Page title="Edit campaign">
      <QueryState query={campaign}>
        {(details) => (
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
        )}
      </QueryState>
    </Page>
  );
}
