import { Alert, Button, Group, NumberInput, Stack } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  getGetBoatSettingsQueryKey,
  useGetBoatSettings,
  useUpdateBoatSettings,
} from "@/api/generated/endpoints/boats/boats";
import { updateBoatSettingsBodyCapacityMax as capacityMax } from "@/api/generated/zod/boats/boats.zod";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { usualBoatCapacity } from "@/features/maps/boats";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

// Orval writes a minimum of 1 inline (.min(1)), with no constant as it has for the maximum.
const capacityMin = 1;

/**
 * What a boat carries (step 51, decision 0022): a unit embarking needs a boat for each this many
 * points, or part. Chart #11's 14, unless the Umpire judges otherwise.
 */
export function BoatSection({ campaignId }: { campaignId: string }) {
  const settings = useGetBoatSettings(campaignId);
  return (
    <Section
      title="Boats"
      description="A unit embarking needs a boat for every so many of its points, or part of them."
    >
      <QueryState query={settings}>
        {(loaded) => (
          <BoatFields key={loaded.capacity} campaignId={campaignId} saved={loaded.capacity} />
        )}
      </QueryState>
    </Section>
  );
}

function BoatFields({ campaignId, saved }: { campaignId: string; saved: number }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateBoatSettings();
  const [capacity, setCapacity] = useState<number | null>(saved);
  const [formError, setFormError] = useState<string | null>(null);
  const valid = capacity !== null && capacity >= capacityMin && capacity <= capacityMax;

  const save = async () => {
    if (!valid) return;
    setFormError(null);
    try {
      await update.mutateAsync({ id: campaignId, data: { capacity } });
      notifications.show({ color: "green", message: "Saved the boat settings." });
      await queryClient.invalidateQueries({ queryKey: getGetBoatSettingsQueryKey(campaignId) });
    } catch (error) {
      setFormError(errorMessage(error, "The boat settings weren't saved. Try again."));
    }
  };

  return (
    <Stack gap="md">
      {formError && (
        <Alert color="red" role="alert">
          {formError}
        </Alert>
      )}
      <NumberInput
        label="Points a boat carries"
        description={`The rules' boat carries ${String(usualBoatCapacity)}. Units already on boats keep theirs.`}
        required
        min={capacityMin}
        max={capacityMax}
        allowDecimal={false}
        allowNegative={false}
        clampBehavior="strict"
        // Its step buttons have no accessible names; arrow keys still step.
        hideControls
        value={capacity ?? ""}
        onChange={(value) => {
          setCapacity(typeof value === "number" ? value : null);
        }}
        error={
          capacity === null
            ? `Enter points from ${String(capacityMin)} to ${String(capacityMax)}.`
            : undefined
        }
      />
      <Group justify="space-between">
        <Button
          variant="subtle"
          onClick={() => {
            setCapacity(usualBoatCapacity);
          }}
        >
          Use the rules&apos; boat
        </Button>
        <Button
          loading={update.isPending}
          disabled={!online || !valid || capacity === saved}
          onClick={() => void save()}
        >
          Save boats
        </Button>
      </Group>
    </Stack>
  );
}
