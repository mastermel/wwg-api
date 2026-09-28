import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";
import { AdminCampaignsPage } from "@/features/admin/AdminCampaignsPage";

export const Route = createFileRoute("/_app/admin/campaigns")({
  // In the URL, so a search can be shared, bookmarked and stepped back through.
  validateSearch: z.object({
    search: z.string().optional().catch(undefined),
    withoutUmpire: z.boolean().optional().catch(undefined),
    page: z.number().int().min(1).optional().catch(undefined),
  }),
  component: function AdminCampaignsRoute() {
    const { search, withoutUmpire, page } = Route.useSearch();
    return (
      <AdminCampaignsPage
        search={search ?? ""}
        withoutUmpire={withoutUmpire === true}
        page={page ?? 1}
      />
    );
  },
});
