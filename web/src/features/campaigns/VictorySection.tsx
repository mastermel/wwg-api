import { Alert, Button, Group, Radio, Stack } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  getGetScoreboardQueryKey,
  getGetVictorySettingsQueryKey,
  useGetVictorySettings,
  useUpdateVictorySettings,
} from "@/api/generated/endpoints/victory/victory";
import type { VictoryPointsMode } from "@/api/generated/model";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

/**
 * Which settlements are worth victory points (step 50, decision 0021): every town, city and
 * fortress by the rules' table, or only those the Umpire gives points in the terrain editor.
 */
export function VictorySection({ campaignId }: { campaignId: string }) {
  const settings = useGetVictorySettings(campaignId);
  return (
    <Section
      title="Victory points"
      description="Which settlements are worth points to the army holding them."
    >
      <QueryState query={settings}>
        {(loaded) => (
          <VictoryFields key={loaded.mode} campaignId={campaignId} saved={loaded.mode} />
        )}
      </QueryState>
    </Section>
  );
}

function VictoryFields({ campaignId, saved }: { campaignId: string; saved: VictoryPointsMode }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateVictorySettings();
  const [mode, setMode] = useState<VictoryPointsMode>(saved);
  const [formError, setFormError] = useState<string | null>(null);

  const save = async () => {
    setFormError(null);
    try {
      await update.mutateAsync({ id: campaignId, data: { mode } });
      notifications.show({ color: "green", message: "Saved the victory points settings." });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getGetVictorySettingsQueryKey(campaignId) }),
        queryClient.invalidateQueries({ queryKey: getGetScoreboardQueryKey(campaignId) }),
      ]);
    } catch (error) {
      setFormError(errorMessage(error, "The victory points settings weren't saved. Try again."));
    }
  };

  return (
    <Stack gap="md">
      {formError && (
        <Alert color="red" role="alert">
          {formError}
        </Alert>
      )}
      <Radio.Group
        label="Settlements worth points"
        value={mode}
        onChange={(value) => {
          setMode(value);
        }}
      >
        <Stack gap="xs" mt="xs">
          <Radio
            value="Rules"
            label="Every settlement, by the rules"
            description="Towns, cities and fortresses on the map are worth the rules' points, unless you set a settlement's own in the terrain editor."
          />
          <Radio
            value="Chosen"
            label="Only those I give points"
            description="A settlement is worth nothing unless you set its points in the terrain editor."
          />
        </Stack>
      </Radio.Group>
      <Group justify="flex-end">
        <Button
          loading={update.isPending}
          disabled={!online || mode === saved}
          onClick={() => void save()}
        >
          Save victory points
        </Button>
      </Group>
    </Stack>
  );
}
