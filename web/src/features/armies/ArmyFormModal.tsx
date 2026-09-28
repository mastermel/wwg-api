import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, Modal, Select, Stack, TextInput } from "@mantine/core";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import type { z } from "zod";
import { CreateArmyBody } from "@/api/generated/zod/armies/armies.zod";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

export type ArmyValues = z.infer<typeof CreateArmyBody>;

interface ArmyFormModalProps {
  title: string;
  submitLabel: string;
  defaultName?: string;
  /** Offer a commander to choose (creating an army); renaming leaves this out. */
  commanders?: { value: string; label: string }[];
  onSubmit: (values: ArmyValues) => Promise<void>;
  onClose: () => void;
}

/** The army name (and, when creating, its commander), in a modal. Mount it only while open. */
export function ArmyFormModal({
  title,
  submitLabel,
  defaultName = "",
  commanders,
  onSubmit,
  onClose,
}: ArmyFormModalProps) {
  const online = useOnline();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<ArmyValues>({
    resolver: zodResolver(CreateArmyBody),
    defaultValues: { name: defaultName, commanderMemberId: null },
  });
  const { errors, isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await onSubmit(values);
      onClose();
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["name", "commanderMemberId"]));
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
