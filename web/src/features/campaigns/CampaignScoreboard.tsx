import { useListArmies } from "@/api/generated/endpoints/armies/armies";
import { useGetScoreboard } from "@/api/generated/endpoints/victory/victory";
import { ScoreboardPanel } from "@/features/maps/ScoreboardPanel";

/** The campaign page's victory points (step 50): the scoreboard, once there's anything to score. */
export function CampaignScoreboard({ campaignId }: { campaignId: string }) {
  const scoreboard = useGetScoreboard(campaignId);
  const armies = useListArmies(campaignId);
  return <ScoreboardPanel scoreboard={scoreboard.data} armies={armies.data ?? []} />;
}
