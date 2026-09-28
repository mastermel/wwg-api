import { createFileRoute } from "@tanstack/react-router";
import { CampaignPage } from "@/features/campaigns/CampaignPage";

export const Route = createFileRoute("/_app/campaigns/$id/")({
  component: function CampaignRoute() {
    return <CampaignPage id={Route.useParams().id} />;
  },
});
