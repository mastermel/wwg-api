import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, Modal, Stack, TextInput } from "@mantine/core";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { RenameUnitBody } from "@/api/generated/zod/units/units.zod";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

type UnitValues = z.infer<typeof RenameUnitBody>;

/** Renames a unit, in a modal. Mount it only while open. */
export function RenameUnitModal({
  name,
  onSubmit,
  onClose,
}: {
  name: string;
  onSubmit: (values: UnitValues) => Promise<void>;
  onClose: () => void;
}) {
  const online = useOnline();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<UnitValues>({
    resolver: zodResolver(RenameUnitBody),
    defaultValues: { name },
  });
  const { errors, isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await onSubmit(values);
      onClose();
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["name"]));
    }
  });

  return (
    <Modal opened onClose={onClose} title="Rename unit" centered>
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
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={isSubmitting} disabled={!online}>
              Save
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
