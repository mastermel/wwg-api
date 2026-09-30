import { createFileRoute } from "@tanstack/react-router";
import { FactionPage } from "@/features/library/FactionPage";

export const Route = createFileRoute("/_app/library/$id")({
  component: function FactionRoute() {
    return <FactionPage id={Route.useParams().id} />;
  },
});
