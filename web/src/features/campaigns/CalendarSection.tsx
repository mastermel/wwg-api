import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Button,
  Group,
  MultiSelect,
  SegmentedControl,
  Stack,
  Text,
  TextInput,
} from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import type { z } from "zod";
import {
  getGetCampaignCalendarQueryKey,
  useGetCampaignCalendar,
  useUpdateCampaignCalendar,
} from "@/api/generated/endpoints/campaigns/campaigns";
import { getListTurnsQueryKey } from "@/api/generated/endpoints/turns/turns";
import type { CampaignCalendarResponse, TurnPart } from "@/api/generated/model";
import { UpdateCampaignCalendarBody } from "@/api/generated/zod/campaigns/campaigns.zod";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { nationOptions } from "@/features/armies/identity/nations";
import {
  rulesAfternoonNations,
  rulesMorningNations,
  turnPartHours,
  turnPartLabels,
} from "@/features/campaigns/calendar";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

type CalendarValues = z.infer<typeof UpdateCampaignCalendarBody>;

// Nations only: "No nation" can't march.
const nations = nationOptions.filter((option) => option.value !== "None");

/**
 * The campaign's calendar (step 45): the first turn's day and time of day, which label every
 * turn, and whose infantry march a flat hex further each Morning or less each Afternoon.
 */
export function CalendarSection({ campaignId }: { campaignId: string }) {
  const calendar = useGetCampaignCalendar(campaignId);
  return (
    <Section
      title="Calendar"
      description="Turns are 8 hours, three to a day: Morning, Afternoon and Night."
    >
      <QueryState query={calendar}>
        {(loaded) => (
          <CalendarForm key={JSON.stringify(loaded)} campaignId={campaignId} calendar={loaded} />
        )}
      </QueryState>
    </Section>
  );
}

function CalendarForm({
  campaignId,
  calendar,
}: {
  campaignId: string;
  calendar: CampaignCalendarResponse;
}) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateCampaignCalendar();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<CalendarValues>({
    resolver: zodResolver(UpdateCampaignCalendarBody),
    defaultValues: {
      startDate: calendar.startDate ?? null,
      firstTurnPart: calendar.firstTurnPart,
      morningNations: [...calendar.morningNations],
      afternoonNations: [...calendar.afternoonNations],
    },
  });
  const { isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await update.mutateAsync({ id: campaignId, data: values });
      notifications.show({ color: "green", message: "Saved the calendar." });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getGetCampaignCalendarQueryKey(campaignId) }),
        queryClient.invalidateQueries({ queryKey: getListTurnsQueryKey(campaignId) }),
      ]);
    } catch (error) {
      setFormError(
        applyServerErrors(error, form.setError, [
          "startDate",
          "morningNations",
          "afternoonNations",
        ]),
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
        <TextInput
          type="date"
          label="The first turn's day"
          description="Optional. Each turn is labelled with its day."
          {...form.register("startDate", {
            setValueAs: (value: string | null) => (value === "" ? null : value),
          })}
        />
        <Controller
          control={form.control}
          name="firstTurnPart"
          render={({ field }) => (
            <Stack gap={4}>
              <Text size="sm" fw={500} id="first-turn-part">
                The first turn&apos;s time of day
              </Text>
              <SegmentedControl
                aria-labelledby="first-turn-part"
                data={(Object.keys(turnPartLabels) as TurnPart[]).map((part) => ({
                  value: part,
                  label: `${turnPartLabels[part]} (${turnPartHours[part]})`,
                }))}
                value={field.value}
                onChange={(value) => {
                  field.onChange(value);
                }}
                fullWidth
                orientation="vertical"
              />
            </Stack>
          )}
        />
        <Controller
          control={form.control}
          name="morningNations"
          render={({ field }) => (
            <MultiSelect
              label="A flat hex further each Morning"
              description="Whose infantry, foot artillery and engineers. The rule book's: France and its usual allies."
              data={nations}
              value={field.value}
              onChange={field.onChange}
              searchable
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Controller
          control={form.control}
          name="afternoonNations"
          render={({ field }) => (
            <MultiSelect
              label="A flat hex less each Afternoon"
              description="The rule book's: Russia and Austria."
              data={nations}
              value={field.value}
              onChange={field.onChange}
              searchable
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Group justify="space-between">
          <Button
            variant="subtle"
            onClick={() => {
              form.setValue("morningNations", [...rulesMorningNations]);
              form.setValue("afternoonNations", [...rulesAfternoonNations]);
            }}
          >
            Use the rule book&apos;s nations
          </Button>
          <Button type="submit" loading={isSubmitting} disabled={!online}>
            Save calendar
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
