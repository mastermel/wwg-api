import { notifications } from "@mantine/notifications";
import { useQueries, useQueryClient } from "@tanstack/react-query";
import {
  getListArmyTurnsQueryKey,
  getListArmyTurnsQueryOptions,
  useGiveOrder,
  useSubmitTurn,
  useUndoOrder,
} from "@/api/generated/endpoints/turns/turns";
import type { ArmySummary, ArmyTurnDetails, GiveOrderRequest } from "@/api/generated/model";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { errorMessage } from "@/lib/errors";

/** An army, and its turn in the open campaign turn (once loaded). */
export interface OpenArmyTurn {
  army: ArmySummary;
  turn: ArmyTurnDetails | undefined;
}

/** The open turn of each army (those a commander commands; every one, for the Umpire). Online only. */
export function useOpenTurns(armies: readonly ArmySummary[]): OpenArmyTurn[] {
  const results = useQueries({
    queries: armies.map((army) =>
      getListArmyTurnsQueryOptions(army.id, { query: { meta: { persist: false } } }),
    ),
  });
  return armies.map((army, index) => ({
    army,
    turn: results[index]?.data?.find((turn) => turn.open),
  }));
}

/**
 * A commander's changes to their army's turn: each confirms itself or says what went wrong, then
 * refreshes the campaign and the army's turns (keyed by the army, so `refreshCampaign` misses them).
 * Each resolves to whether it worked.
 */
export function useOrders(campaignId: string) {
  const queryClient = useQueryClient();
  const give = useGiveOrder();
  const undo = useUndoOrder();
  const submit = useSubmitTurn();

  const run = async (
    armyId: string,
    change: () => Promise<unknown>,
    done: string,
    failed: string,
  ) => {
    try {
      await change();
      notifications.show({ color: "green", message: done });
      return true;
    } catch (error) {
      notifications.show({ color: "red", message: errorMessage(error, failed) });
      return false;
    } finally {
      await Promise.all([
        refreshCampaign(queryClient, campaignId),
        queryClient.invalidateQueries({ queryKey: getListArmyTurnsQueryKey(armyId) }),
      ]);
    }
  };

  return {
    busy: give.isPending || undo.isPending || submit.isPending,
    give: (
      armyId: string,
      turnId: string,
      unit: { id: string; name: string },
      order: GiveOrderRequest,
    ) =>
      run(
        armyId,
        () => give.mutateAsync({ id: turnId, unitId: unit.id, data: order }),
        order.kind === "Hold" ? `${unit.name} will hold.` : `${unit.name} will move.`,
        `${unit.name}'s order couldn't be saved. Try again.`,
      ),
    undo: (armyId: string, turnId: string, unit: { id: string; name: string }) =>
      run(
        armyId,
        () => undo.mutateAsync({ id: turnId, unitId: unit.id }),
        `Took back ${unit.name}'s order.`,
        `${unit.name}'s order couldn't be taken back. Try again.`,
      ),
    submit: (armyId: string, turn: ArmyTurnDetails, armyName: string) =>
      run(
        armyId,
        () => submit.mutateAsync({ id: turn.id }),
        `Submitted ${armyName}'s turn ${String(turn.turn)}.`,
        `${armyName}'s turn couldn't be submitted. Try again.`,
      ),
  };
}

/** The Umpire's latest word on a Draft turn: why it was sent back or reopened, if it was. */
export function reviewOf(turn: ArmyTurnDetails) {
  const last = turn.history.at(-1);
  return turn.status === "Draft" && (last?.kind === "SentBack" || last?.kind === "Reverted")
    ? last
    : undefined;
}
