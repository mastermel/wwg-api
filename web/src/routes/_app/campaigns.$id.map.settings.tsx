import { createFileRoute } from "@tanstack/react-router";
import { MapSettingsPage } from "@/features/maps/MapSettingsPage";

export const Route = createFileRoute("/_app/campaigns/$id/map/settings")({
  component: function MapSettingsRoute() {
    return <MapSettingsPage campaignId={Route.useParams().id} />;
  },
});
