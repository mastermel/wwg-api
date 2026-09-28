import { createFileRoute } from "@tanstack/react-router";
import { AdminUserPage } from "@/features/admin/AdminUserPage";

export const Route = createFileRoute("/_app/admin/users/$id")({
  component: function AdminUserRoute() {
    return <AdminUserPage id={Route.useParams().id} />;
  },
});
