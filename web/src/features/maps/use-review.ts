import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useApproveTurn, useStartNextTurn } from "@/api/generated/endpoints/turns/turns";
import type { ArmyTurnDetails } from "@/api/generated/model";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { errorMessage } from "@/lib/errors";

/**
 * The Umpire's changes to the open turn: approving an army's turn, and starting the next. Each
 * confirms itself or says what went wrong, then refreshes the campaign and every army's turns,
 * and resolves to whether it worked.
 */
export function useReview(campaignId: string) {
  const queryClient = useQueryClient();
  const approve = useApproveTurn();
  const startNext = useStartNextTurn();

  const refresh = () =>
    Promise.all([
      refreshCampaign(queryClient, campaignId),
      // Every army's: they're keyed by the army, which refreshCampaign doesn't reach.
      queryClient.invalidateQueries({
        predicate: (query) =>
          typeof query.queryKey[0] === "string" &&
          /^\/api\/armies\/[^/]+\/turns$/.test(query.queryKey[0]),
      }),
    ]);

  const run = async (change: () => Promise<unknown>, done: string, failed: string) => {
    try {
      await change();
      notifications.show({ color: "green", message: done });
      return true;
    } catch (error) {
      notifications.show({ color: "red", message: errorMessage(error, failed) });
      return false;
    } finally {
      await refresh();
    }
  };

  return {
    busy: approve.isPending || startNext.isPending,
    approve: (turn: ArmyTurnDetails, armyName: string) =>
      run(
        () => approve.mutateAsync({ id: turn.id }),
        `Approved ${armyName}'s turn ${String(turn.turn)}.`,
        `${armyName}'s turn couldn't be approved. Try again.`,
      ),
    startNext: (number: number) =>
      run(
        () => startNext.mutateAsync({ id: campaignId }),
        `Turn ${String(number)} has started.`,
        "The next turn couldn't be started. Try again.",
      ),
  };
}
