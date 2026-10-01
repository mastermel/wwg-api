import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Box,
  Button,
  Group,
  Modal,
  MultiSelect,
  Select,
  Stack,
  TextInput,
} from "@mantine/core";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { useListFactions } from "@/api/generated/endpoints/library/library";
import type { ArmyColor } from "@/api/generated/model";
import { CreateArmyBody, UpdateArmyBody } from "@/api/generated/zod/armies/armies.zod";
import { armyColors, armyColorVar } from "@/features/armies/identity/army-colors";
import { NationFlag } from "@/features/armies/identity/NationFlag";
import { nationOptions } from "@/features/armies/identity/nations";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

// Everything an army is; the commander only when it's created (the army page changes it). The
// form always sends its factions (the API also takes null, for "leave them").
const ArmyForm = UpdateArmyBody.extend({
  commanderMemberId: CreateArmyBody.shape.commanderMemberId,
  // Every army is on one of the campaign's two sides (decision 0017).
  sideId: z.uuid({ error: "Choose a side." }),
  factionIds: z.array(z.uuid()),
});

export type ArmyValues = z.infer<typeof ArmyForm>;

const colorOptions = (Object.keys(armyColors) as ArmyColor[]).map((color) => ({
  value: color,
  label: armyColors[color].label,
}));

interface ArmyFormModalProps {
  title: string;
  submitLabel: string;
  defaultValues: ArmyValues;
  /** Offer a commander to choose (creating an army); editing leaves this out. */
  commanders?: { value: string; label: string }[];
  /** The campaign's sides, to put the army on one. */
  sides: { value: string; label: string }[];
  onSubmit: (values: ArmyValues) => Promise<void>;
  onClose: () => void;
}

/**
 * An army's name, side, colour, nation and library factions (and, when creating, its commander),
 * in a modal.
 * Mount it only while open.
 */
export function ArmyFormModal({
  title,
  submitLabel,
  defaultValues,
  commanders,
  sides,
  onSubmit,
  onClose,
}: ArmyFormModalProps) {
  const online = useOnline();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<ArmyValues>({ resolver: zodResolver(ArmyForm), defaultValues });
  const { errors, isSubmitting } = form.formState;
  const color = useWatch({ control: form.control, name: "color" });
  // A Manager may have added one since: the library is fetched afresh each time the form opens.
  const factions = useListFactions({ query: { refetchOnMount: "always" } });

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await onSubmit(values);
      onClose();
    } catch (error) {
      setFormError(
        applyServerErrors(error, form.setError, [
          "name",
          "commanderMemberId",
          "sideId",
          "color",
          "nation",
          "factionIds",
        ]),
      );
    }
  });

  return (
    <Modal opened onClose={onClose} title={title} centered>
      <form onSubmit={(event) => void submit(event)} noValidate>
        <Stack>
          {formError && (
            <Alert color="red" role="alert">
              {formError}
            </Alert>
          )}
          <TextInput
            label="Name"
            required
            data-autofocus
            error={errors.name?.message}
            {...form.register("name", { setValueAs: (value: string) => value.trim() })}
          />
          {commanders && (
            <Controller
              control={form.control}
              name="commanderMemberId"
              render={({ field }) => (
                <Select
                  label="Commander"
                  description="Optional. Players who don't command an army yet."
                  placeholder={commanders.length ? "Unassigned" : "No Players are free"}
                  data={commanders}
                  value={field.value}
                  onChange={field.onChange}
                  clearable
                  disabled={commanders.length === 0}
                  error={errors.commanderMemberId?.message}
                  comboboxProps={{ withinPortal: false }}
                />
              )}
            />
          )}
          <Controller
            control={form.control}
            name="sideId"
            render={({ field }) => (
              <Select
                label="Side"
                required
                data={sides}
                value={field.value || null}
                onChange={(value) => {
                  if (value) field.onChange(value);
                }}
                allowDeselect={false}
                error={errors.sideId?.message}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="color"
            render={({ field }) => (
              <Select
                label="Colour"
                description="On the map and beside its name. Armies can share one."
                data={colorOptions}
                value={field.value}
                onChange={(value) => {
                  if (value) field.onChange(value);
                }}
                allowDeselect={false}
                leftSection={<Swatch color={field.value} />}
                renderOption={({ option }) => (
                  <Group gap="xs" wrap="nowrap">
                    <Swatch color={option.value} />
                    {option.label}
                  </Group>
                )}
                error={errors.color?.message}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="nation"
            render={({ field }) => (
              <Select
                label="Nation"
                description="Its flag. No nation is a plain flag in its colour."
                data={nationOptions}
                value={field.value}
                onChange={(value) => {
                  if (value) field.onChange(value);
                }}
                allowDeselect={false}
                searchable
                leftSection={<NationFlag nation={field.value} plainColor={armyColorVar(color)} />}
                leftSectionWidth={40}
                renderOption={({ option }) => (
                  <Group gap="xs" wrap="nowrap">
                    <NationFlag nation={option.value} plainColor={armyColorVar(color)} />
                    {option.label}
                  </Group>
                )}
                error={errors.nation?.message}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="factionIds"
            render={({ field }) => (
              <MultiSelect
                label="Factions"
                description="The library factions it takes its units from. One it has units from stays."
                placeholder={field.value.length ? undefined : "None yet"}
                data={(factions.data ?? []).map((f) => ({ value: f.id, label: f.name }))}
                value={field.value}
                onChange={field.onChange}
                searchable
                nothingFoundMessage="No such faction"
                error={errors.factionIds?.message}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={isSubmitting} disabled={!online}>
              {submitLabel}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

/** A small square of an army colour. */
function Swatch({ color }: { color: ArmyColor }) {
  return (
    <Box
      w={14}
      h={14}
      style={{ borderRadius: 3, background: armyColorVar(color), flexShrink: 0 }}
      aria-hidden
    />
  );
}
