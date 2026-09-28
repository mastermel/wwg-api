import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";
import { AdminUsersPage } from "@/features/admin/AdminUsersPage";

export const Route = createFileRoute("/_app/admin/users/")({
  // In the URL, so a search can be shared, bookmarked and stepped back through.
  validateSearch: z.object({
    search: z.string().optional().catch(undefined),
    page: z.number().int().min(1).optional().catch(undefined),
  }),
  component: function AdminUsersRoute() {
    const { search, page } = Route.useSearch();
    return <AdminUsersPage search={search ?? ""} page={page ?? 1} />;
  },
});
