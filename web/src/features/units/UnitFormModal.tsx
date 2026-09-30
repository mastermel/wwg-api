import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, Modal, NumberInput, Select, Stack, TextInput } from "@mantine/core";
import { useState } from "react";
import { Controller, useForm, type DefaultValues } from "react-hook-form";
import { z } from "zod";
import { UnitType } from "@/api/generated/model";
import {
  CreateArmyUnitBody,
  createArmyUnitBodyFightingFactorMax,
  createArmyUnitBodyPointsMax,
  createArmyUnitBodyPointsMin,
} from "@/api/generated/zod/army-units/army-units.zod";
import { unitTypeOptions } from "@/features/units/unit-types";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

// Orval writes a minimum of 1 inline (.min(1)), with no constant as it has for the others.
const ffMin = 1;

// The generated schema, with messages people can act on.
const UnitForm = CreateArmyUnitBody.extend({
  type: z.enum(Object.values(UnitType), { error: "Choose a type." }),
  fightingFactor: z
    .int({
      error: `Enter an FF from ${String(ffMin)} to ${String(createArmyUnitBodyFightingFactorMax)}.`,
    })
    .min(ffMin)
    .max(createArmyUnitBodyFightingFactorMax),
  points: z
    .int({
      error: `Enter points from ${String(createArmyUnitBodyPointsMin)} to ${String(createArmyUnitBodyPointsMax)}.`,
    })
    .min(createArmyUnitBodyPointsMin)
    .max(createArmyUnitBodyPointsMax),
});

export type UnitValues = z.infer<typeof UnitForm>;

const fields = ["name", "type", "fightingFactor", "points"] as const;

/** A NumberInput's value as the form's number: empty becomes undefined, so it's "required". */
const toNumber = (value: number | string) => (typeof value === "number" ? value : undefined);

interface UnitFormModalProps {
  title: string;
  submitLabel: string;
  defaultValues?: DefaultValues<UnitValues>;
  onSubmit: (values: UnitValues) => Promise<void>;
  onClose: () => void;
}

/** A unit's name, type, Fighting Factor and points, in a modal. Mount it only while open. */
export function UnitFormModal({
  title,
  submitLabel,
  defaultValues = { name: "", points: 0 },
  onSubmit,
  onClose,
}: UnitFormModalProps) {
  const online = useOnline();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<UnitValues>({ resolver: zodResolver(UnitForm), defaultValues });
  const { errors, isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await onSubmit(values);
      onClose();
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, fields));
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
          <Controller
            control={form.control}
            name="type"
            render={({ field }) => (
              <Select
                label="Type"
                required
                placeholder="Choose a type"
                data={unitTypeOptions}
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                allowDeselect={false}
                error={errors.type?.message}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Group grow align="flex-start">
            <Controller
              control={form.control}
              name="fightingFactor"
              render={({ field }) => (
                <NumberInput
                  label="Fighting Factor (FF)"
                  required
                  min={ffMin}
                  max={createArmyUnitBodyFightingFactorMax}
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
                  error={errors.fightingFactor?.message}
                />
              )}
            />
            <Controller
              control={form.control}
              name="points"
              render={({ field }) => (
                <NumberInput
                  label="Points"
                  required
                  min={createArmyUnitBodyPointsMin}
                  max={createArmyUnitBodyPointsMax}
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
                  error={errors.points?.message}
                />
              )}
            />
          </Group>
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
