import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, Stack, Textarea, TextInput } from "@mantine/core";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { CreateCampaignBody } from "@/api/generated/zod/campaigns/campaigns.zod";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

export type CampaignValues = z.infer<typeof CreateCampaignBody>;

const trim = (value: string) => value.trim();

/** The name and description form, shared by "new campaign" and "edit campaign". */
export function CampaignForm({
  defaultValues = { name: "", description: "" },
  submitLabel,
  onSubmit,
  onCancel,
}: {
  defaultValues?: CampaignValues;
  submitLabel: string;
  onSubmit: (values: CampaignValues) => Promise<void>;
  onCancel: () => void;
}) {
  const online = useOnline();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<CampaignValues>({
    resolver: zodResolver(CreateCampaignBody),
    defaultValues,
  });
  const { errors, isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await onSubmit(values);
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["name", "description"]));
    }
  });

  return (
    <form onSubmit={(event) => void submit(event)} noValidate>
      <Stack maw={640}>
        {formError && (
          <Alert color="red" role="alert">
            {formError}
          </Alert>
        )}
        <TextInput
          label="Name"
          required
          error={errors.name?.message}
          {...form.register("name", { setValueAs: trim })}
        />
        <Textarea
          label="Description"
          description="Optional: the setting, the rules you're using, anything players should know."
          autosize
          minRows={3}
          error={errors.description?.message}
          {...form.register("description", { setValueAs: (v: string | null) => v?.trim() ?? "" })}
        />
        <Group>
          <Button type="submit" loading={isSubmitting} disabled={!online}>
            {submitLabel}
          </Button>
          <Button variant="default" onClick={onCancel}>
            Cancel
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
