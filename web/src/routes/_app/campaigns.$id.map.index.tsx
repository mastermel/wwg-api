import { createFileRoute } from "@tanstack/react-router";
import { MapPage } from "@/features/maps/MapPage";

export const Route = createFileRoute("/_app/campaigns/$id/map/")({
  component: function MapRoute() {
    return <MapPage campaignId={Route.useParams().id} />;
  },
});
