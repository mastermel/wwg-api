import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, MultiSelect, NumberInput, Stack, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import {
  getGetCampaignConcentrationQueryKey,
  useGetCampaignConcentration,
  useUpdateCampaignConcentration,
} from "@/api/generated/endpoints/campaigns/campaigns";
import type { CampaignConcentrationResponse } from "@/api/generated/model";
import {
  UpdateCampaignConcentrationBody,
  updateCampaignConcentrationBodyInfantryLimitMax as limitMax,
} from "@/api/generated/zod/campaigns/campaigns.zod";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import {
  rulesCavalryLimit,
  rulesInfantryLimit,
  usualCavalryTypes,
  usualInfantryTypes,
} from "@/features/campaigns/concentration";
import { unitTypeLabels, unitTypeOptions } from "@/features/units/unit-types";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

// Orval writes a minimum of 1 inline (.min(1)), with no constant as it has for the maximum.
const limitMin = 1;

const limit = z
  .int({ error: `Enter points from ${String(limitMin)} to ${String(limitMax)}.` })
  .min(limitMin)
  .max(limitMax);

// The generated schema, with messages people can act on.
const ConcentrationForm = UpdateCampaignConcentrationBody.extend({
  infantryLimit: limit,
  cavalryLimit: limit,
});

type ConcentrationValues = z.infer<typeof ConcentrationForm>;

/** A NumberInput's value as the form's number: empty becomes undefined, so it's "required". */
const toNumber = (value: number | string) => (typeof value === "number" ? value : undefined);

/**
 * The campaign's concentration settings (step 46, decision 0017): the most points of infantry
 * and of cavalry a side may have in a hex, and which unit types count towards each.
 */
export function ConcentrationSection({ campaignId }: { campaignId: string }) {
  const concentration = useGetCampaignConcentration(campaignId);
  return (
    <Section
      title="Concentration"
      description="The most points a side may have in a hex. Doubled in a city or a fortress."
    >
      <QueryState query={concentration}>
        {(loaded) => (
          <ConcentrationFormFields
            key={JSON.stringify(loaded)}
            campaignId={campaignId}
            concentration={loaded}
          />
        )}
      </QueryState>
    </Section>
  );
}

function ConcentrationFormFields({
  campaignId,
  concentration,
}: {
  campaignId: string;
  concentration: CampaignConcentrationResponse;
}) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateCampaignConcentration();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<ConcentrationValues>({
    resolver: zodResolver(ConcentrationForm),
    defaultValues: {
      infantryTypes: [...concentration.infantryTypes],
      cavalryTypes: [...concentration.cavalryTypes],
      infantryLimit: concentration.infantryLimit,
      cavalryLimit: concentration.cavalryLimit,
    },
  });
  const { errors, isSubmitting } = form.formState;
  const [infantryTypes, cavalryTypes] = useWatch({
    control: form.control,
    name: ["infantryTypes", "cavalryTypes"],
  });
  // A type counts towards one limit at most: each list offers only what the other hasn't taken.
  const options = (taken: readonly string[]) =>
    unitTypeOptions.filter((option) => !taken.includes(option.value));
  const free = unitTypeOptions.filter(
    (option) => !infantryTypes.includes(option.value) && !cavalryTypes.includes(option.value),
  );

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await update.mutateAsync({ id: campaignId, data: values });
      notifications.show({ color: "green", message: "Saved the concentration settings." });
      await queryClient.invalidateQueries({
        queryKey: getGetCampaignConcentrationQueryKey(campaignId),
      });
    } catch (error) {
      setFormError(
        applyServerErrors(error, form.setError, [
          "infantryTypes",
          "cavalryTypes",
          "infantryLimit",
          "cavalryLimit",
        ]),
      );
    }
  });

  const limitField = (name: "infantryLimit" | "cavalryLimit", label: string) => (
    <Controller
      control={form.control}
      name={name}
      render={({ field }) => (
        <NumberInput
          label={label}
          required
          min={limitMin}
          max={limitMax}
          allowDecimal={false}
          allowNegative={false}
          clampBehavior="strict"
          // Its step buttons have no accessible names; arrow keys still step.
          hideControls
          value={field.value}
          onChange={(value) => {
            field.onChange(toNumber(value));
          }}
          onBlur={field.onBlur}
          error={errors[name]?.message}
        />
      )}
    />
  );

  return (
    <form onSubmit={(event) => void submit(event)} noValidate>
      <Stack gap="md">
        {formError && (
          <Alert color="red" role="alert">
            {formError}
          </Alert>
        )}
        <Group grow align="flex-start">
          {limitField("infantryLimit", "Infantry limit (points)")}
          {limitField("cavalryLimit", "Cavalry limit (points)")}
        </Group>
        <Controller
          control={form.control}
          name="infantryTypes"
          render={({ field }) => (
            <MultiSelect
              label="Counted as infantry"
              data={options(cavalryTypes)}
              value={field.value}
              onChange={field.onChange}
              error={errors.infantryTypes?.message}
              searchable
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Controller
          control={form.control}
          name="cavalryTypes"
          render={({ field }) => (
            <MultiSelect
              label="Counted as cavalry"
              data={options(infantryTypes)}
              value={field.value}
              onChange={field.onChange}
              error={errors.cavalryTypes?.message}
              searchable
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Text size="sm" c="dimmed">
          Free (counted towards neither):{" "}
          {free.length === 0
            ? "none"
            : free.map((option) => unitTypeLabels[option.value]).join(", ")}
          .
        </Text>
        <Group justify="space-between">
          <Button
            variant="subtle"
            onClick={() => {
              form.setValue("infantryTypes", [...usualInfantryTypes]);
              form.setValue("cavalryTypes", [...usualCavalryTypes]);
              form.setValue("infantryLimit", rulesInfantryLimit);
              form.setValue("cavalryLimit", rulesCavalryLimit);
            }}
          >
            Use the usual settings
          </Button>
          <Button type="submit" loading={isSubmitting} disabled={!online}>
            Save concentration
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
