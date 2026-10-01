import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import {
  getGetSupplyQueryKey,
  getListDepotsQueryKey,
  useCreateDepot,
  useDeleteDepot,
  useUpdateDepot,
} from "@/api/generated/endpoints/supply/supply";
import type { DepotResponse, SaveDepotRequest } from "@/api/generated/model";
import { errorMessage } from "@/lib/errors";

/** "Charleroi", or "the depot" when it has no name. */
export const depotName = (depot: { name?: string | null }) => depot.name ?? "the depot";

/**
 * The Umpire's changes to depots (step 48a): each confirms itself or says what went wrong, then
 * refreshes the campaign's depots. Each resolves to whether it worked.
 */
export function useDepots(campaignId: string) {
  const queryClient = useQueryClient();
  const create = useCreateDepot();
  const update = useUpdateDepot();
  const remove = useDeleteDepot();

  const run = async (change: () => Promise<unknown>, done: string, failed: string) => {
    try {
      await change();
      notifications.show({ color: "green", message: done });
      return true;
    } catch (error) {
      notifications.show({ color: "red", message: errorMessage(error, failed) });
      return false;
    } finally {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getListDepotsQueryKey(campaignId) }),
        // Supply follows the depots (step 48).
        queryClient.invalidateQueries({ queryKey: getGetSupplyQueryKey(campaignId) }),
      ]);
    }
  };

  return {
    busy: create.isPending || update.isPending || remove.isPending,
    create: (armyId: string, data: SaveDepotRequest) =>
      run(
        () => create.mutateAsync({ id: armyId, data }),
        `Placed ${depotName(data)}.`,
        "The depot couldn't be placed. Try again.",
      ),
    update: (depot: DepotResponse, data: SaveDepotRequest) =>
      run(
        () => update.mutateAsync({ id: depot.id, data }),
        `Saved ${depotName(data)}.`,
        "The depot couldn't be saved. Try again.",
      ),
    remove: (depot: DepotResponse) =>
      run(
        () => remove.mutateAsync({ id: depot.id }),
        `Removed ${depotName(depot)}.`,
        "The depot couldn't be removed. Try again.",
      ),
  };
}
