import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";
import { CampaignsPage } from "@/features/campaigns/CampaignsPage";

export const Route = createFileRoute("/_app/campaigns/")({
  validateSearch: z.object({ page: z.number().int().min(1).optional().catch(undefined) }),
  component: function CampaignsRoute() {
    return <CampaignsPage page={Route.useSearch().page ?? 1} />;
  },
});
