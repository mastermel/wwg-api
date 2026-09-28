import { createFileRoute } from "@tanstack/react-router";
import { NewCampaignPage } from "@/features/campaigns/NewCampaignPage";

export const Route = createFileRoute("/_app/campaigns/new")({
  component: NewCampaignPage,
});
