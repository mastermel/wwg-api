import { createFileRoute } from "@tanstack/react-router";
import { ArmyPage } from "@/features/armies/ArmyPage";

export const Route = createFileRoute("/_app/campaigns/$id/armies/$armyId")({
  component: function ArmyRoute() {
    const { id, armyId } = Route.useParams();
    return <ArmyPage campaignId={id} armyId={armyId} />;
  },
});
