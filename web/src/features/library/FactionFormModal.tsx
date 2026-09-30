import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, Modal, Select, Stack, TextInput } from "@mantine/core";
import { useState } from "react";
import { Controller, useForm, type DefaultValues } from "react-hook-form";
import type { z } from "zod";
import { CreateFactionBody } from "@/api/generated/zod/library/library.zod";
import { NationFlag } from "@/features/armies/identity/NationFlag";
import { nationOptions } from "@/features/armies/identity/nations";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

export type FactionValues = z.infer<typeof CreateFactionBody>;

// A faction has no colour of its own: no nation is a plain grey flag.
const plainColor = "var(--mantine-color-gray-5)";

interface FactionFormModalProps {
  title: string;
  submitLabel: string;
  defaultValues?: DefaultValues<FactionValues>;
  onSubmit: (values: FactionValues) => Promise<void>;
  onClose: () => void;
}

/** A library faction's name and nation, in a modal. Mount it only while open. */
export function FactionFormModal({
  title,
  submitLabel,
  defaultValues = { name: "", nation: "None" },
  onSubmit,
  onClose,
}: FactionFormModalProps) {
  const online = useOnline();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<FactionValues>({
    resolver: zodResolver(CreateFactionBody),
    defaultValues,
  });
  const { errors, isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await onSubmit(values);
      onClose();
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["name", "nation"]));
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
            description="E.g. French, or Russian Guard."
            required
            data-autofocus
            error={errors.name?.message}
            {...form.register("name", { setValueAs: (value: string) => value.trim() })}
          />
          <Controller
            control={form.control}
            name="nation"
            render={({ field }) => (
              <Select
                label="Nation"
                description="Its flag, if it has one."
                data={nationOptions}
                value={field.value}
                onChange={(value) => {
                  if (value) field.onChange(value);
                }}
                allowDeselect={false}
                searchable
                leftSection={<NationFlag nation={field.value} plainColor={plainColor} />}
                leftSectionWidth={40}
                renderOption={({ option }) => (
                  <Group gap="xs" wrap="nowrap">
                    <NationFlag nation={option.value} plainColor={plainColor} />
                    {option.label}
                  </Group>
                )}
                error={errors.nation?.message}
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
