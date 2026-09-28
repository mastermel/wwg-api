import { createFileRoute } from "@tanstack/react-router";
import { EditCampaignPage } from "@/features/campaigns/EditCampaignPage";

export const Route = createFileRoute("/_app/campaigns/$id/edit")({
  component: function EditCampaignRoute() {
    return <EditCampaignPage id={Route.useParams().id} />;
  },
});
