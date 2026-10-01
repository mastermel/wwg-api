import { Select } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useListArmies } from "@/api/generated/endpoints/armies/armies";
import {
  getGetScoreboardQueryKey,
  useGetScoreboard,
  useSetHolding,
} from "@/api/generated/endpoints/victory/victory";
import type { Hex } from "@/features/maps/hex-grid";
import { hexName } from "@/features/maps/hex-grid";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

const noOne = "none";

/**
 * Who holds a settlement, for the Umpire in the terrain editor (step 50, decision 0021): who
 * starts with it, or a correction. Saved as soon as it's chosen.
 */
export function HolderField({ campaignId, hex }: { campaignId: string; hex: Hex }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const armies = useListArmies(campaignId);
  const scoreboard = useGetScoreboard(campaignId, { query: { meta: { persist: false } } });
  const setHolding = useSetHolding();
  const holder = scoreboard.data?.settlements.find((s) => s.q === hex.q && s.r === hex.r)?.armyId;

  return (
    <Select
      label="Held by"
      description="Who holds it now (or starts with it): its victory points are theirs."
      data={[
        { value: noOne, label: "No one" },
        ...(armies.data ?? []).map((a) => ({ value: a.id, label: a.name })),
      ]}
      value={holder ?? noOne}
      allowDeselect={false}
      disabled={!online || setHolding.isPending || !scoreboard.data}
      onChange={(value) => {
        if (!value) return;
        const armyId = value === noOne ? null : value;
        void setHolding
          .mutateAsync({ id: campaignId, q: hex.q, r: hex.r, data: { armyId } })
          .then(() => {
            notifications.show({
              color: "green",
              message: `${hexName(hex)} is held by ${armies.data?.find((a) => a.id === armyId)?.name ?? "no one"}.`,
            });
          })
          .catch((error: unknown) => {
            notifications.show({
              color: "red",
              message: errorMessage(error, "Who holds it couldn't be saved. Try again."),
            });
          })
          .finally(
            () =>
              void queryClient.invalidateQueries({
                queryKey: getGetScoreboardQueryKey(campaignId),
              }),
          );
      }}
      comboboxProps={{ withinPortal: false }}
    />
  );
}
