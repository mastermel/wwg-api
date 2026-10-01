import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, MultiSelect, NumberInput, Stack } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import {
  getGetSupplySettingsQueryKey,
  useGetSupplySettings,
  useUpdateSupplySettings,
} from "@/api/generated/endpoints/supply/supply";
import type { CampaignSupplySettingsResponse } from "@/api/generated/model";
import { UpdateSupplySettingsBody } from "@/api/generated/zod/supply/supply.zod";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { nationOptions } from "@/features/armies/identity/nations";
import {
  maxSupplyReach,
  usualExemptTypes,
  usualOffTheLandNations,
  usualSupplyReach,
} from "@/features/campaigns/supply";
import { unitTypeOptions } from "@/features/units/unit-types";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

// The generated schema, with a message people can act on.
const SupplyForm = UpdateSupplySettingsBody.extend({
  reach: z
    .int({ error: `Enter 0 to ${String(maxSupplyReach)} hexes.` })
    .min(0)
    .max(maxSupplyReach),
});

type SupplyValues = z.infer<typeof SupplyForm>;

// Nations only: "No nation" can't live off the land.
const nations = nationOptions.filter((option) => option.value !== "None");

/**
 * The campaign's supply settings (step 48, decision 0019): how far from its army's roads and
 * waterways a unit stays supplied, the unit types that don't need supply, and the nations whose
 * units may live off the land.
 */
export function SupplySection({ campaignId }: { campaignId: string }) {
  const settings = useGetSupplySettings(campaignId);
  return (
    <Section
      title="Supply"
      description="Each army is supplied from its own depots along roads and waterways."
    >
      <QueryState query={settings}>
        {(loaded) => (
          <SupplyFormFields
            key={JSON.stringify(loaded)}
            campaignId={campaignId}
            settings={loaded}
          />
        )}
      </QueryState>
    </Section>
  );
}

function SupplyFormFields({
  campaignId,
  settings,
}: {
  campaignId: string;
  settings: CampaignSupplySettingsResponse;
}) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateSupplySettings();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<SupplyValues>({
    resolver: zodResolver(SupplyForm),
    defaultValues: {
      reach: settings.reach,
      exemptTypes: [...settings.exemptTypes],
      offTheLandNations: [...settings.offTheLandNations],
    },
  });
  const { errors, isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await update.mutateAsync({ id: campaignId, data: values });
      notifications.show({ color: "green", message: "Saved the supply settings." });
      await queryClient.invalidateQueries({ queryKey: getGetSupplySettingsQueryKey(campaignId) });
    } catch (error) {
      setFormError(
        applyServerErrors(error, form.setError, ["reach", "exemptTypes", "offTheLandNations"]),
      );
    }
  });

  return (
    <form onSubmit={(event) => void submit(event)} noValidate>
      <Stack gap="md">
        {formError && (
          <Alert color="red" role="alert">
            {formError}
          </Alert>
        )}
        <Controller
          control={form.control}
          name="reach"
          render={({ field }) => (
            <NumberInput
              label="Supply reach (hexes)"
              description="How far from its army's roads and waterways a unit stays supplied."
              required
              min={0}
              max={maxSupplyReach}
              allowDecimal={false}
              allowNegative={false}
              clampBehavior="strict"
              // Its step buttons have no accessible names; arrow keys still step.
              hideControls
              value={field.value}
              onChange={(value) => {
                field.onChange(typeof value === "number" ? value : undefined);
              }}
              onBlur={field.onBlur}
              error={errors.reach?.message}
            />
          )}
        />
        <Controller
          control={form.control}
          name="exemptTypes"
          render={({ field }) => (
            <MultiSelect
              label="Don't need supply"
              data={unitTypeOptions}
              value={field.value}
              onChange={field.onChange}
              error={errors.exemptTypes?.message}
              searchable
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Controller
          control={form.control}
          name="offTheLandNations"
          render={({ field }) => (
            <MultiSelect
              label="May live off the land"
              description="Their units are never out of supply, but their side's hex is held to half the concentration."
              data={nations}
              value={field.value}
              onChange={field.onChange}
              error={errors.offTheLandNations?.message}
              searchable
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Group justify="space-between">
          <Button
            variant="subtle"
            onClick={() => {
              form.setValue("reach", usualSupplyReach);
              form.setValue("exemptTypes", [...usualExemptTypes]);
              form.setValue("offTheLandNations", [...usualOffTheLandNations]);
            }}
          >
            Use the usual settings
          </Button>
          <Button type="submit" loading={isSubmitting} disabled={!online}>
            Save supply
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
