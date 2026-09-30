import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, Modal, Stack, TextInput } from "@mantine/core";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { CreateSideBody } from "@/api/generated/zod/sides/sides.zod";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

type SideValues = z.infer<typeof CreateSideBody>;

interface SideFormModalProps {
  title: string;
  submitLabel: string;
  defaultName?: string;
  onSubmit: (values: SideValues) => Promise<void>;
  onClose: () => void;
}

/** A side's name, in a modal. Mount it only while open. */
export function SideFormModal({
  title,
  submitLabel,
  defaultName = "",
  onSubmit,
  onClose,
}: SideFormModalProps) {
  const online = useOnline();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<SideValues>({
    resolver: zodResolver(CreateSideBody),
    defaultValues: { name: defaultName },
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
            description="E.g. Coalition, or French Empire."
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
              {submitLabel}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
