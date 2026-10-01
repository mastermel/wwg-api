import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import {
  useApproveTurn,
  useRevertTurn,
  useSendBackTurn,
  useStartNextTurn,
  useSubmitTurn,
} from "@/api/generated/endpoints/turns/turns";
import type {
  ArmyTurnDetails,
  AttritionLossRequest,
  ReviewTurnRequest,
  SightingRequest,
} from "@/api/generated/model";
import { getGetSupplyQueryKey } from "@/api/generated/endpoints/supply/supply";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { errorMessage } from "@/lib/errors";

/**
 * The Umpire's changes to the open turn: submitting an army's turn for it, approving, sending back and reverting an army's turn,
 * and starting the next. Each confirms itself or says what went wrong, then refreshes the
 * campaign and every army's turns. Approve and start resolve to whether they worked; send back and
 * revert throw, so their form can show why.
 */
export function useReview(campaignId: string) {
  const queryClient = useQueryClient();
  const approve = useApproveTurn();
  const sendBack = useSendBackTurn();
  const revert = useRevertTurn();
  const startNext = useStartNextTurn();
  const submit = useSubmitTurn();

  const refresh = () =>
    Promise.all([
      refreshCampaign(queryClient, campaignId),
      queryClient.invalidateQueries({ queryKey: getGetSupplyQueryKey(campaignId) }),
      // Every army's turns and marches, and every unit's points history: they're keyed by the
      // army or unit, which refreshCampaign doesn't reach.
      queryClient.invalidateQueries({
        predicate: (query) =>
          typeof query.queryKey[0] === "string" &&
          /^\/api\/(armies\/[^/]+\/(turns|marches)|army-units\/[^/]+\/points|campaigns\/[^/]+\/(sightings|reports|couriers))$/.test(
            query.queryKey[0],
          ),
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

  const review = async (
    kind: "send-back" | "revert",
    turn: ArmyTurnDetails,
    armyName: string,
    data: ReviewTurnRequest,
  ) => {
    try {
      await (kind === "send-back" ? sendBack : revert).mutateAsync({ id: turn.id, data });
      notifications.show({
        color: "green",
        message: `${kind === "send-back" ? "Sent back" : "Reopened"} ${armyName}'s turn ${String(turn.turn)}.`,
      });
    } finally {
      await refresh();
    }
  };

  return {
    busy: approve.isPending || startNext.isPending || submit.isPending,
    // On the army's behalf (decision 0011): the way past an army with no commander.
    submit: (turn: ArmyTurnDetails, armyName: string) =>
      run(
        () => submit.mutateAsync({ id: turn.id }),
        `Submitted ${armyName}'s turn ${String(turn.turn)}.`,
        `${armyName}'s turn couldn't be submitted. Try again.`,
      ),
    approve: (turn: ArmyTurnDetails, armyName: string) =>
      run(
        () => approve.mutateAsync({ id: turn.id }),
        `Approved ${armyName}'s turn ${String(turn.turn)}.`,
        `${armyName}'s turn couldn't be approved. Try again.`,
      ),
    review,
    // With the attrition the closing turn cost, as the Umpire confirmed it (step 47).
    startNext: (
      number: number,
      attrition: AttritionLossRequest[] = [],
      sightings: SightingRequest[] = [],
    ) =>
      run(
        () => startNext.mutateAsync({ id: campaignId, data: { attrition, sightings } }),
        `Turn ${String(number)} has started.`,
        "The next turn couldn't be started. Try again.",
      ),
  };
}
