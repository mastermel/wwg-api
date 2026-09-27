import { createFileRoute } from "@tanstack/react-router";
import { CampaignsPage } from "@/features/campaigns/CampaignsPage";

export const Route = createFileRoute("/campaigns/")({
  component: CampaignsPage,
});
