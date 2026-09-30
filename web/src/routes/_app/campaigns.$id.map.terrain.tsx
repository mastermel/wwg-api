import { createFileRoute } from "@tanstack/react-router";
import { TerrainPage } from "@/features/maps/TerrainPage";

export const Route = createFileRoute("/_app/campaigns/$id/map/terrain")({
  component: function TerrainRoute() {
    return <TerrainPage campaignId={Route.useParams().id} />;
  },
});
